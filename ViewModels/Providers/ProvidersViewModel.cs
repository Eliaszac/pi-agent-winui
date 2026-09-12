using System.Text.Json;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Providers;

public sealed class ProvidersViewModel : ObservableObject
{
    private readonly IProviderService service;
    public OllamaSetupViewModel? Ollama { get; }
    public ProvidersViewModel(IProviderService service, OllamaSetupViewModel? ollama = null)
    {
        this.service = service;
        Ollama = ollama;
        if (ollama is not null) ollama.InventoryChanged += Filter;
        Filter();
    }
    private IReadOnlyList<ProviderCardViewModel> cards = [];
    private string search = "";
    private bool configuredOnly;
    private bool busy;
    private string error = "";
    public bool HasInventory { get; private set; }
    public bool HasConfiguredProvider => Cards.Any(card => card.Provider.Configured);
    public IReadOnlyList<ProviderCardViewModel> Cards { get => cards; private set { SetProperty(ref cards, value); Filter(); } }
    public IReadOnlyList<ProviderCardViewModel> VisibleCards { get; private set; } = [];
    public string Search { get => search; set { if (SetProperty(ref search, value)) Filter(); } }
    public bool ConfiguredOnly { get => configuredOnly; set { if (SetProperty(ref configuredOnly, value)) Filter(); } }
    public bool NoResults => VisibleCards.Count == 0 && !IsBusy;
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(CanInteract)); OnPropertyChanged(nameof(NoResults)); } }
    public bool CanInteract => !IsBusy;
    public string Error { get => error; private set => SetProperty(ref error, value); }

    public async Task RefreshAsync()
    {
        if (IsBusy || IsRefreshing) return;
        IsRefreshing = true;
        try { await RefreshCatalogAsync(); }
        finally { IsRefreshing = false; }
    }

    public bool IsRefreshing { get; private set; }
    private bool catalogRefreshPending;
    public async Task RefreshCatalogAsync()
    {
        if (IsBusy) { catalogRefreshPending = true; return; }
        try { await RunAsync("list"); }
        catch (Exception exception) { Error = exception is OperationCanceledException ? "Refresh cancelled. Try again." : exception.Message; }
    }

    private void Filter()
    {
        var all = Cards.AsEnumerable();
        if (Ollama is { } ollama)
        {
            var models = ollama.Registrations.SelectMany(registration => registration.ModelIds.Select(id =>
                new Models.Pi.PiProviderModel(id, id, 0, 0, false, Array.Empty<string>()))).ToArray();
            var configured = Cards.Any(card => IsOllamaId(card.Id) && card.Provider.Configured);
            var card = new ProviderCardViewModel(new("ollama", "Ollama", configured, "", "", false, false, false, "", models), true,
                ollama.LocalStatus, ollama.SyncSummary);
            all = new[] { card }.Concat(all.Where(item => !IsOllamaId(item.Id)));
        }
        var query = Search.Trim();
        VisibleCards = all.Where(card => (!ConfiguredOnly || card.Provider.Configured) && (query.Length == 0
            || $"{card.Name} {card.Id} {string.Join(' ', card.Provider.Models.Select(model => model.Name + " " + model.Id))}"
                .Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(card => ModelProviderPresentation.SortPriority(card.Id)).ToArray();
        OnPropertyChanged(nameof(VisibleCards)); OnPropertyChanged(nameof(NoResults));
    }
    private static bool IsOllamaId(string id) => id == "ollama" || id.StartsWith("ollama-remote-", StringComparison.Ordinal);

    public async Task RunAsync(string action, ProviderCardViewModel? card = null, string? method = null,
        Func<JsonElement, CancellationToken, Task<string?>>? prompt = null, Action<JsonElement>? notify = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy) throw new InvalidOperationException("Wait for the current provider operation to finish.");
        IsBusy = true;
        Error = "";
        try
        {
            var result = await Task.Run(() => service.RunAsync(action, card?.Id, method, prompt, notify, cancellationToken), cancellationToken);
            Cards = result.OrderByDescending(provider => provider.Configured).ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .Select(provider => new ProviderCardViewModel(provider)).ToArray();
            HasInventory = true;
            OnPropertyChanged(nameof(HasInventory));
            OnPropertyChanged(nameof(HasConfiguredProvider));
        }
        catch
        {
            HasInventory = false;
            OnPropertyChanged(nameof(HasInventory));
            throw;
        }
        finally
        {
            IsBusy = false;
            if (catalogRefreshPending) { catalogRefreshPending = false; _ = RefreshCatalogAsync(); }
        }
    }
}
