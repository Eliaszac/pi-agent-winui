using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>Native MCP setup state, including partial saves and cancellable connection checks.</summary>
public sealed class McpSetupViewModel : ObservableObject, IDisposable
{
    private readonly CapabilityImportServices services;
    private readonly string? initialJson;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private JsonObject? expected;
    private JsonObject? definition;
    private bool busy, saved, ready;
    private int authIndex;
    private string message = "Loading settings…", folder = "", endpoint = "";
    public string Name { get; }
    public bool IsObsidianSetup => Name == "obsidian" && definition is null && ready;
    public bool IsHttp => definition?["url"] is not null;
    public bool IsBusy => busy;
    public bool CanAct => ready && !busy;
    public bool IsSaved => saved;
    public string Endpoint => endpoint;
    public string Folder { get => folder; set { SetProperty(ref folder, value); } }
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public int AuthIndex { get => authIndex; set { if (SetProperty(ref authIndex, value)) Notify(); } }
    public bool NeedsToken => IsHttp && authIndex == 2;
    public string PrimaryLabel => IsHttp && authIndex == 1 ? "Sign in & connect" : NeedsToken ? "Connect" : saved ? "Check connection" : "Add & connect";
    public string AuthHelp => NeedsToken ? "Paste a token to save it securely in the OS credential store. Leave blank to use existing credentials. Pasting replaces this server's existing bearer-token settings; the token is never written to JSON."
        : IsHttp && authIndex == 1 ? "Your browser opens for sign-in. Credentials stay in the MCP adapter's OS credential store. You can cancel here at any time."
        : IsHttp && authIndex == 0 ? "Use the server's existing authentication settings and advertised support. If it requires sign-in or a token, choose the matching method."
        : IsHttp ? "Connect without an Authorization header. Only choose this for a server that does not require authentication."
        : Name == "obsidian" ? "The local server starts when you check the connection. Obsidian needs a vault folder and Node.js/npm; no sign-in is required."
        : "This local server starts using its saved command and environment. Any required credentials are managed by the server's own setup.";
    public Func<JsonElement, CancellationToken, Task<string?>>? Prompt { get; set; }

    public McpSetupViewModel(string name, string? initialJson, CapabilityImportServices services)
    { Name = name; this.initialJson = initialJson; this.services = services; }

    public async Task InitializeAsync()
    {
        try
        {
            if (services.McpSetup is null) throw new InvalidOperationException("MCP setup is unavailable.");
            expected = await services.McpSetup.ReadAsync(Name);
            saved = expected is not null;
            definition = expected?.DeepClone().AsObject() ?? (initialJson is null ? null : JsonNode.Parse(initialJson)!["mcpServers"]![Name]!.DeepClone().AsObject());
            if (definition is null && Name != "obsidian") throw new InvalidOperationException("This server comes from another configuration source. Manage its settings in that source; it cannot be overwritten here.");
            endpoint = definition?["url"] is JsonValue url && url.TryGetValue<string>(out var address) ? ConversationErrors.Describe(address).Message : "Local process · stdio";
            authIndex = definition is null ? 0 : McpAuthenticationSettings.Detect(definition) switch { "oauth" => 1, "bearer" => 2, "none" => 3, _ => 0 };
            ready = true;
            Message = saved ? "Settings are saved. Check the connection or update authentication below." : "Add this integration globally. Existing conversations load it after restarting Pi desktop.";
        }
        catch (Exception exception) { Message = SafeError(exception); }
        Notify();
    }

    public async Task RunAsync(bool connect, string secret = "")
    {
        if (!CanAct) return;
        busy = true; operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); Notify();
        try
        {
            if (connect && NeedsToken && !saved && string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("Enter an API token to connect, or choose Save only to finish authentication later.");
            if (definition is null)
                definition = JsonNode.Parse(McpQuickConfiguration.BuildObsidian(Folder))!["mcpServers"]!["obsidian"]!.AsObject();
            var candidate = definition.DeepClone().AsObject();
            var desired = authIndex switch { 1 => "oauth", 2 => "bearer", 3 => "none", _ => "auto" };
            if (IsHttp && (secret.Length > 0 || (desired != "auto" && desired != McpAuthenticationSettings.Detect(candidate))))
                candidate = McpAuthenticationSettings.Apply(candidate, desired);
            Message = "Saving settings…";
            await services.McpSetup!.SaveAsync(Name, expected, candidate);
            expected = candidate.DeepClone().AsObject(); definition = candidate; saved = true;
            operation.Token.ThrowIfCancellationRequested();
            if (secret.Length > 0)
            {
                if (services.McpTokens is null) throw new InvalidOperationException("Token storage is unavailable.");
                Message = "Saving token securely…";
                await services.McpTokens.SaveAsync(Name, secret, operation.Token);
            }
            if (!connect) { Message = "Saved. Restart Pi desktop to load this integration. Authentication and connectivity have not been checked."; return; }
            if (services.McpConnections is null) throw new InvalidOperationException("Connection checks are unavailable.");
            Message = IsHttp && authIndex == 1 ? "Opening browser sign-in…" : "Checking connection…";
            var result = await services.McpConnections.RunAsync(Name, IsHttp && authIndex == 1,
                text => Message = text, Prompt ?? ((_, _) => Task.FromResult<string?>(null)), operation.Token);
            Message = result.Connected ? $"Connection verified · {result.ToolCount} tools found. Restart Pi desktop to load the saved settings in your conversations."
                : "Saved, but not connected. " + result.Message;
            if (!result.Connected && result.State == "needs-auth")
            { authIndex = 1; Message = "Sign-in required. Choose Sign in & connect to continue in your browser."; }
        }
        catch (OperationCanceledException) { Message = saved ? "Cancelled. Saved settings are kept; you can retry here. A completed sign-in may already have saved credentials." : "Cancelled. No connection was verified."; }
        catch (Exception exception) { Message = (saved ? "Settings saved. " : "") + SafeError(exception); }
        finally { busy = false; operation.Dispose(); operation = null; Notify(); }
    }

    public void Cancel() => operation?.Cancel();
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
    private static string SafeError(Exception exception) => exception is ArgumentException or InvalidOperationException or IOException or TimeoutException
        ? ConversationErrors.Describe(exception.Message).Message : "Couldn't finish setup. Check the MCP Adapter installation and try again.";
    private void Notify()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(CanAct), nameof(IsSaved), nameof(IsObsidianSetup), nameof(IsHttp), nameof(Endpoint), nameof(AuthIndex), nameof(NeedsToken), nameof(PrimaryLabel), nameof(AuthHelp) }) OnPropertyChanged(name);
    }
}
