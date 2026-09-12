using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Projects;

public sealed class CreateProjectViewModel : TargetLocationViewModel
{
    private readonly ProjectService service;
    private string name = "";
    private string errorMessage = "";
    private bool isBusy;
    public CreateProjectViewModel(ProjectService service)
    {
        this.service = service;
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Name) or nameof(Path) or nameof(Host) or nameof(WslDistribution) or nameof(RepositoryUrl)
                or nameof(TargetKindIndex) or nameof(SourceIndex) or nameof(IsBusy) or nameof(SshSecret) or nameof(SshKeyPath) or nameof(SshAuthenticationIndex)) OnPropertyChanged(nameof(CanSubmit));
        };
    }
    public string Name { get => name; set => SetProperty(ref name, value); }
    public string ErrorMessage { get => errorMessage; set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => ErrorMessage.Length > 0;
    public bool IsBusy { get => isBusy; private set { if (SetProperty(ref isBusy, value)) OnPropertyChanged(nameof(IsEditable)); } }
    public bool IsEditable => !IsBusy;
    public bool CanSubmit => !IsBusy && !string.IsNullOrWhiteSpace(Name) && IsLocationComplete;
    public async Task<Project?> TryCreateAsync()
    {
        if (!CanSubmit) return null;
        var savedName = Name;
        var target = CreateTarget(Guid.NewGuid(), IsLocal ? "This computer" : SelectedHost.Trim());
        var url = CloneRepository ? RepositoryUrl : null;
        var secret = IsSsh && SshSecret.Length > 0 ? SshSecret : null;
        IsBusy = true; ErrorMessage = "";
        try { return await Task.Run(() => service.CreateWithTargetAsync(savedName, target, url, sshSecret: secret)); }
        catch (Exception exception) { ErrorMessage = exception is ArgumentException or IOException ? exception.Message : ProjectErrorMessage.From(exception); return null; }
        finally { IsBusy = false; }
    }
}
