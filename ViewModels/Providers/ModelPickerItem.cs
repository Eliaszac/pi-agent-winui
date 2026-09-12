using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Providers;

public sealed class ModelPickerItem(PiModel model, bool selected, bool favorite) : ObservableObject
{
    private bool isFavorite = favorite;
    public PiModel Model { get; } = model;
    public string Name => Model.Name;
    public string Detail => $"{ModelProviderPresentation.Get(Model.Provider).Name} · {Model.Id}";
    public bool IsSelected { get; } = selected;
    public string SelectionLabel => $"{Name}, {Detail}" + (IsSelected ? ", selected" : "");
    public bool IsFavorite
    {
        get => isFavorite;
        set
        {
            if (!SetProperty(ref isFavorite, value)) return;
            OnPropertyChanged(nameof(FavoriteGlyph));
            OnPropertyChanged(nameof(FavoriteLabel));
        }
    }
    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";
    public string FavoriteLabel => $"{(IsFavorite ? "Remove" : "Add")} {Name} {(IsFavorite ? "from" : "to")} favorites";
}
