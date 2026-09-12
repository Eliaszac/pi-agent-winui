using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Providers;

/// <summary>Filters a Pi-owned catalog; favorites are presentation preferences only.</summary>
public sealed class ModelPickerViewModel(ModelFavoritesStore store) : ObservableObject
{
    private IReadOnlyList<PiModel> models = [];
    private IReadOnlySet<ModelIdentity> favorites = new HashSet<ModelIdentity>();
    private ModelIdentity? selected;
    private ModelProviderItem? provider;
    private string search = "";
    private string error = "";
    private bool busy;
    public IReadOnlyList<ModelProviderItem> Providers { get; private set; } = [];
    public IReadOnlyList<ModelPickerItem> Items { get; private set; } = [];
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(CanFavorite)); } }
    public bool CanFavorite => !IsBusy;
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public string Search { get => search; set { if (SetProperty(ref search, value)) Filter(); } }
    public ModelProviderItem? SelectedProvider
    {
        get => provider;
        set { if (SetProperty(ref provider, value)) { Search = ""; Filter(); } }
    }
    public string Heading => string.IsNullOrWhiteSpace(Search) ? SelectedProvider?.Name ?? "Models" : "Search results";
    public bool IsEmpty => Items.Count == 0;
    public string EmptyText => !string.IsNullOrWhiteSpace(Search) ? "No models match your search."
        : SelectedProvider?.Id == "" ? "Star models from any provider to find them here."
        : "No models available from this provider.";

    public void Update(IReadOnlyList<PiModel> available, PiModel? current)
    {
        models = available.DistinctBy(ModelIdentity.From).ToArray();
        selected = current is null ? null : ModelIdentity.From(current);
        var ids = models.Select(model => model.Provider).Distinct(StringComparer.Ordinal)
            .OrderBy(ModelProviderPresentation.SortPriority)
            .ThenBy(id => ModelProviderPresentation.Get(id).Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (!Providers.Skip(1).Select(item => item.Id).SequenceEqual(ids) || Providers.Count == 0)
        {
            Providers = new[] { new ModelProviderItem("") }.Concat(ids.Select(id => new ModelProviderItem(id))).ToArray();
            OnPropertyChanged(nameof(Providers));
        }
        provider = Providers.FirstOrDefault(item => item.Id == provider?.Id)
            ?? Providers.FirstOrDefault(item => item.Id == current?.Provider) ?? Providers[0];
        OnPropertyChanged(nameof(SelectedProvider));
        Filter();
    }

    public async Task OpenAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = "";
        try
        {
            favorites = await store.ReadAsync();
            SelectedProvider = models.Any(model => favorites.Contains(ModelIdentity.From(model))) ? Providers.FirstOrDefault()
                : Providers.FirstOrDefault(item => item.Id == selected?.Provider) ?? Providers.FirstOrDefault();
            Search = "";
            Filter();
        }
        catch (Exception) { Error = "Couldn't load favorites. You can still select a model."; }
        finally { IsBusy = false; }
    }

    public async Task ToggleFavoriteAsync(ModelPickerItem item)
    {
        if (IsBusy || !models.Any(model => ModelIdentity.From(model) == ModelIdentity.From(item.Model))) return;
        IsBusy = true;
        Error = "";
        try
        {
            favorites = await store.SetAsync(ModelIdentity.From(item.Model), !item.IsFavorite);
            foreach (var row in Items) row.IsFavorite = favorites.Contains(ModelIdentity.From(row.Model));
            if (SelectedProvider?.Id == "" && string.IsNullOrWhiteSpace(Search)) Filter();
        }
        catch (Exception) { Error = "Couldn't save favorites. Try again."; }
        finally { IsBusy = false; }
    }

    private void Filter()
    {
        var term = Search.Trim();
        Items = models.Where(model => term.Length > 0
                ? $"{model.Name} {model.Id} {model.Provider} {ModelProviderPresentation.Get(model.Provider).Name}".Contains(term, StringComparison.OrdinalIgnoreCase)
                : SelectedProvider?.Id == "" ? favorites.Contains(ModelIdentity.From(model)) : model.Provider == SelectedProvider?.Id)
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase).ThenBy(model => model.Provider, StringComparer.Ordinal)
            .ThenBy(model => model.Id, StringComparer.Ordinal)
            .Select(model => new ModelPickerItem(model, ModelIdentity.From(model) == selected, favorites.Contains(ModelIdentity.From(model)))).ToArray();
        foreach (var name in new[] { nameof(Items), nameof(Heading), nameof(IsEmpty), nameof(EmptyText) }) OnPropertyChanged(name);
    }
}
