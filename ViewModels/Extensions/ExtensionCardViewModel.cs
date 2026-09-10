using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Extensions;

public sealed class ExtensionCardViewModel : ObservableObject
{
    private string status = "Checking global configuration…";
    private bool needsSetup;
    public ExtensionDefinition Definition { get; }
    public string Attribution => $"Third-party · by {Definition.Author}";
    public string VersionLabel => $"Supported version {Definition.Version}";
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool NeedsSetup { get => needsSetup; private set => SetProperty(ref needsSetup, value); }
    public AsyncRelayCommand RefreshCommand { get; }

    public ExtensionCardViewModel(ExtensionDefinition definition)
    {
        Definition = definition;
        RefreshCommand = new AsyncRelayCommand(async _ =>
        {
            var installation = await Task.Run(definition.CheckInstallation);
            NeedsSetup = installation.NeedsSetup;
            Status = installation.Status;
        }, exception =>
        {
            NeedsSetup = true;
            Status = "Couldn't check configuration: " + exception.Message;
        });
    }
}
