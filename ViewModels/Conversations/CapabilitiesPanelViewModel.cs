using System.ComponentModel;
using System.Text.Json;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>Read-only, selection-scoped skill inventory and MCP presentation.</summary>
public sealed class CapabilitiesPanelViewModel : ObservableObject
{
    private readonly Func<(string Status, bool NeedsSetup)> installationState;
    private readonly Func<Task<IReadOnlyList<McpServerStatus>>> readConfigured;
    private IReadOnlyList<McpServerStatus> configured = [];
    private readonly HashSet<string> removedServers = new(StringComparer.Ordinal);
    public void ReportRemoved(string name)
    {
        removedServers.Add(name);
        configured = configured.Where(server => server.Name != name).ToArray();
        NotifyLists();
    }
    public CapabilitiesPanelViewModel(Func<(string Status, bool NeedsSetup)>? installationState = null,
        Func<Task<IReadOnlyList<McpServerStatus>>>? readConfigured = null)
    {
        this.installationState = installationState ?? (() => McpAdapterSupport.GetInstallationState());
        this.readConfigured = readConfigured ?? new Services.Pi.McpConfiguredInventory(PermissionModesSupport.AgentDirectory).ReadAsync;
    }
    private ConversationViewModel? owner;
    private int revision;
    private bool isOpen;
    private string search = "";
    private IReadOnlyList<AvailableSkill> skills = [];
    private bool adapterLoaded;
    private string adapterSetup = "Install MCP Adapter from Extensions to use MCP servers.";
    private string message = "Open a conversation to see its available skills.";
    private bool loading;
    private bool wasConnected;
    public InstructionsPanelViewModel Instructions { get; } = new();
    public bool IsOpen { get => isOpen; set { if (SetProperty(ref isOpen, value) && value) _ = RefreshAsync(); } }
    public string Search { get => search; set { if (SetProperty(ref search, value)) { Instructions.Filter(value); NotifyLists(); } } }
    public string Message { get => message; private set { SetProperty(ref message, value); OnPropertyChanged(nameof(HasMessage)); } }
    public bool HasMessage => Message.Length > 0;
    public bool IsLoading { get => loading; private set => SetProperty(ref loading, value); }
    public IReadOnlyList<AvailableSkill> Skills => skills.Where(item => Matches(item.Name) || Matches(item.Description)).ToArray();
    public IReadOnlyList<McpServerStatus> Servers => (owner?.IsConnected == true ? owner.McpStatus?.Servers ?? [] : [])
        .Concat(configured).DistinctBy(item => item.Name).Where(item => !removedServers.Contains(item.Name) && Matches(item.Name))
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    public bool HasNoSkills => !IsLoading && Skills.Count == 0;
    public IEnumerable<string> KnownServerNames => configured.Select(server => server.Name).Concat(owner?.McpStatus?.Servers.Select(server => server.Name) ?? []).Where(name => !removedServers.Contains(name)).Distinct();
    public bool HasNoServers => Servers.Count == 0;
    public bool ShowMcpSetup => owner?.McpStatus is null;
    public bool HasMcpMessage => McpMessage.Length > 0;
    public string McpMessage => owner?.IsConnected != true ? "Conversation disconnected · server status unavailable."
        : owner.McpStatus is not null ? (owner.McpStatus.Servers.Count == 0 && configured.Count == 0 ? "No MCP servers configured in this conversation."
            : Servers.Count == 0 ? "No MCP servers match this view." : "")
        : !adapterLoaded ? "MCP Adapter is not loaded in this conversation. Install it, then restart Pi desktop."
        : adapterSetup;

    public void Select(ConversationViewModel? conversation)
    {
        if (ReferenceEquals(owner, conversation)) return;
        if (owner is not null) owner.PropertyChanged -= OnOwnerChanged;
        owner = conversation;
        wasConnected = owner?.IsConnected == true;
        Instructions.Select(owner?.Instructions, wasConnected, owner?.WorkingDirectory);
        if (owner is not null) owner.PropertyChanged += OnOwnerChanged;
        revision++;
        skills = [];
        adapterLoaded = false;
        IsLoading = false;
        Message = owner?.IsConnected == true ? "" : "Open a connected conversation to see its available skills.";
        NotifyLists();
        if (IsOpen) _ = RefreshAsync();
    }

    private void OnOwnerChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ConversationViewModel.Instructions))
            Instructions.Select(owner?.Instructions, owner?.IsConnected == true, owner?.WorkingDirectory);
        if (args.PropertyName == nameof(ConversationViewModel.McpStatus)) NotifyLists();
        if (args.PropertyName != nameof(ConversationViewModel.IsConnected)) return;
        var nowConnected = owner?.IsConnected == true;
        if (wasConnected == nowConnected) return;
        wasConnected = nowConnected;
        Instructions.Select(owner?.Instructions, nowConnected, owner?.WorkingDirectory);
        if (owner?.IsConnected != true)
        {
            revision++; skills = []; IsLoading = false;
            Message = "Conversation disconnected · skill inventory unavailable.";
        }
        else if (IsOpen && !IsLoading && skills.Count == 0) _ = RefreshAsync();
        NotifyLists();
    }

    public async Task RefreshAsync()
    {
        var current = owner;
        var request = ++revision;
        IsLoading = true;
        Message = "";
        try
        {
            try { var saved = await readConfigured(); if (request != revision) return; configured = saved; removedServers.ExceptWith(saved.Select(server => server.Name)); }
            catch (Exception) { if (request == revision) Message = "Couldn't read the saved MCP configuration. Check mcp.json for invalid JSON or file access errors."; }
            NotifyLists();
            if (current?.IsConnected != true) return;
            var data = await current.ReadCapabilitiesAsync();
            if (request != revision || !ReferenceEquals(owner, current)) return;
            var commands = PiJson.Field(data, "commands");
            skills = CapabilityParser.Skills(commands);
            if (current.CanUseCommands && commands.ValueKind == JsonValueKind.Array && commands.EnumerateArray().Any(item => PiJson.Text(item, "name") == "pi-gui-instructions"))
            {
                try { await current.ReadInstructionsAsync(); }
                catch (Exception) { Instructions.ReportUnavailable(); }
                if (request != revision) return;
            }
            await Instructions.RefreshAsync();
            if (request != revision) return;
            adapterLoaded = commands.ValueKind == JsonValueKind.Array && commands.EnumerateArray().Any(item =>
                PiJson.Text(item, "source") == "extension" && PiJson.Text(item, "name") == "mcp");
            if (adapterLoaded && current.McpStatus is null)
            {
                try
                {
                    var installation = await Task.Run(installationState);
                    if (request != revision) return;
                    adapterSetup = installation.NeedsSetup
                        ? "Update MCP Adapter for live status, then restart Pi desktop. " + installation.Status
                        : "Waiting for adapter status. If it does not arrive, restart Pi desktop to load the updated adapter.";
                }
                catch (Exception)
                {
                    if (request == revision) adapterSetup = "Live status unavailable. Check the adapter setup and restart Pi desktop.";
                }
            }
        }
        catch (Exception) { if (request == revision) Message = "Couldn't refresh the inventory. Try Refresh after Pi is ready."; }
        finally
        {
            if (request == revision) { IsLoading = false; NotifyLists(); }
        }
    }

    public void ReportError(string text) => Message = text;

    private bool Matches(string text) => text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    private void NotifyLists()
    {
        foreach (var property in new[] { nameof(Skills), nameof(Servers), nameof(HasNoSkills), nameof(HasNoServers),
            nameof(McpMessage), nameof(HasMcpMessage), nameof(ShowMcpSetup) }) OnPropertyChanged(property);
    }
}
