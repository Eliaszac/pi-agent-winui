using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Providers;

/// <summary>Synchronizes local and explicitly saved Ollama connections.</summary>
public sealed class OllamaSetupViewModel(OllamaClient client, OllamaModelImporter importer, Action modelsChanged,
    CancellationToken lifetime = default) : ObservableObject
{
    private readonly OllamaModelSync sync = new(client, importer);
    private string address = OllamaEndpoint.Local;
    private bool busy;
    private string message = "";
    public string LocalStatus { get; private set; } = "Checking local server…";
    public string SyncSummary { get; private set; } = "New tool-capable models are imported automatically.";
    public int LocalModelCount { get; private set; }
    public IReadOnlyList<OllamaRegistration> Registrations { get; private set; } = [];
    public IReadOnlyList<Uri> SavedEndpoints { get; private set; } = [];
    public IReadOnlyList<OllamaModelChoice> Models { get; private set; } = [];
    public string Address
    {
        get => address;
        set { if (SetProperty(ref address, value)) { Models = []; NotifyModels(); Message = ""; OnPropertyChanged(nameof(CanConnect)); } }
    }
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(CanInteract)); OnPropertyChanged(nameof(CanConnect)); } }
    public bool CanInteract => !IsBusy;
    public bool CanConnect => !IsBusy && !string.IsNullOrWhiteSpace(Address);
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public bool HasModels => Models.Count > 0;
    public event Action? InventoryChanged;

    public async Task SyncKnownAsync(CancellationToken token = default)
    {
        if (IsBusy || lifetime.IsCancellationRequested || token.IsCancellationRequested) return;
        IsBusy = true;
        var added = 0;
        var warnings = new List<string>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            foreach (var endpoint in await importer.KnownEndpointsAsync(timeout.Token))
            {
                try
                {
                    var result = await sync.SyncAsync(endpoint, false, timeout.Token);
                    added += result.Added;
                    Apply(result);
                    if (result.Warning is { } warning) warnings.Add(warning);
                }
                catch (Exception exception) when (!timeout.IsCancellationRequested)
                {
                    if (endpoint == OllamaEndpoint.Parse(OllamaEndpoint.Local) && exception is System.Net.Http.HttpRequestException or OperationCanceledException)
                        LocalStatus = "Local server unavailable";
                    else warnings.Add($"Couldn't sync {endpoint.Host}. Check its connection and Pi configuration.");
                }
            }
            await ReloadAsync(timeout.Token);
            if (added > 0 || warnings.Count > 0)
                SyncSummary = string.Join(" ", (added > 0 ? new[] { $"Added {added} models automatically. Start a new conversation for updated context settings." } : Array.Empty<string>()).Concat(warnings));
        }
        catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested) SyncSummary = "Ollama sync timed out or was cancelled. Refresh to retry."; }
        catch (Exception) { SyncSummary = "Couldn't sync Ollama. Check the saved connections and Pi configuration."; }
        finally { Complete(added); }
    }

    public async Task ConnectAsync(CancellationToken token = default)
    {
        if (!CanConnect) return;
        IsBusy = true;
        var added = 0;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime);
        try
        {
            var result = await sync.SyncAsync(OllamaEndpoint.Parse(Address), true, linked.Token);
            added = result.Added;
            Apply(result);
            await ReloadAsync(linked.Token);
            Message = result.Warning ?? (added > 0 ? $"Added {added} models. Start a new conversation for updated context settings."
                : result.ModelCount == 0 ? "Connected. Installed models will appear automatically." : "Connected and up to date. New tool-capable models will appear automatically.");
            SyncSummary = Message;
        }
        catch (ArgumentException exception) { Message = exception.Message; }
        catch (OperationCanceledException) { Message = "Connection cancelled or timed out. Refresh to check registration."; }
        catch (Exception) { Message = "Couldn't sync this server. Check its address, access settings and Pi configuration."; }
        finally { Complete(added); }
    }

    private void Apply(OllamaSyncResult result)
    {
        if (result.Endpoint == OllamaEndpoint.Parse(OllamaEndpoint.Local))
        {
            LocalModelCount = result.ModelCount;
            LocalStatus = result.ModelCount == 0 ? "Local server found · no models installed" : $"Local server found · {result.ModelCount} models";
        }
        Uri displayed;
        try { displayed = OllamaEndpoint.Parse(Address); }
        catch (ArgumentException) { return; }
        if (displayed != result.Endpoint) return;
        var inspected = result.NewModels.ToDictionary(model => model.Id);
        var previous = Models.ToDictionary(row => row.Name);
        Models = result.Names.Select(name => inspected.TryGetValue(name, out var model)
            ? new OllamaModelChoice(model, model.Tools && model.Error is null)
            : previous.GetValueOrDefault(name) ?? new OllamaModelChoice(new(name, false, false, false, null), true)).ToArray();
        NotifyModels();
        if (result.Added > 0 || result.Warning is not null) Message = result.Warning ?? $"Added {result.Added} models automatically.";
    }

    private async Task ReloadAsync(CancellationToken token)
    {
        Registrations = await importer.ReadAsync(token);
        var endpoints = await importer.KnownEndpointsAsync(token);
        if (!SavedEndpoints.SequenceEqual(endpoints)) { SavedEndpoints = endpoints; OnPropertyChanged(nameof(SavedEndpoints)); }
        OnPropertyChanged(nameof(Registrations));
    }

    private void Complete(int added)
    {
        IsBusy = false;
        OnPropertyChanged(nameof(LocalStatus)); OnPropertyChanged(nameof(SyncSummary));
        InventoryChanged?.Invoke();
        if (added > 0 && !lifetime.IsCancellationRequested)
        {
            try { modelsChanged(); }
            catch (Exception) { Message = "Models saved. Restart Pi desktop to refresh the model list."; }
        }
    }

    private void NotifyModels() { OnPropertyChanged(nameof(Models)); OnPropertyChanged(nameof(HasModels)); }
}
