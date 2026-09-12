using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Providers;

public sealed record ModelProviderItem(string Id)
{
    public string Name => Id.Length == 0 ? "Favorites" : ModelProviderPresentation.Get(Id).Name;
}
