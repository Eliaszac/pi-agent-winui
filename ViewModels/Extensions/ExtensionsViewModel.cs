using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Extensions;

public sealed class ExtensionsViewModel : ObservableObject
{
    private string error = "";
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public IReadOnlyList<ExtensionCardViewModel> Cards { get; }
    private IReadOnlyList<InstalledExtension> otherExtensions = [];
    public IReadOnlyList<InstalledExtension> OtherExtensions { get => otherExtensions; private set { SetProperty(ref otherExtensions, value); OnPropertyChanged(nameof(HasOtherExtensions)); } }
    public bool HasOtherExtensions => OtherExtensions.Count > 0;
    public AsyncRelayCommand RefreshCommand { get; }

    public ExtensionsViewModel(IEnumerable<ExtensionDefinition>? definitions = null, Func<IReadOnlyList<InstalledExtension>>? discoverOthers = null)
    {
        Cards = (definitions ?? SupportedExtensions.All).Select(definition => new ExtensionCardViewModel(definition)).ToArray();
        RefreshCommand = new AsyncRelayCommand(async _ =>
        {
            Error = "";
            await Task.WhenAll(Cards.Select(card => card.RefreshCommand.ExecuteAsync()));
            OtherExtensions = await Task.Run(() => discoverOthers is null ? new InstalledExtensionDiscovery().Discover() : discoverOthers());
        }, exception => Error = exception.Message);
    }
}
