using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Projects;

public class TargetLocationViewModel : ObservableObject
{
    private int kind;
    private int source;
    private string host = "";
    private string distribution = "";
    private string path = "";
    private string repositoryUrl = "";
    private int sshAuthenticationIndex;
    private string sshKeyPath = "";
    private string sshSecret = "";
    public int SshAuthenticationIndex { get => sshAuthenticationIndex; set { if (SetProperty(ref sshAuthenticationIndex, value)) NotifyLocation(); } }
    public bool IsPasswordAuthentication => IsSsh && SshAuthenticationIndex == 1;
    public bool IsKeyAuthentication => IsSsh && SshAuthenticationIndex == 0;
    public string SshKeyPath { get => sshKeyPath; set => SetProperty(ref sshKeyPath, value); }
    [System.Text.Json.Serialization.JsonIgnore]
    public string SshSecret { get => sshSecret; set => SetProperty(ref sshSecret, value); }
    public string SshSecretLabel => IsPasswordAuthentication ? "SSH password" : "Key passphrase (optional)";
    public string SshSecretHint => IsPasswordAuthentication ? "Saved securely in Windows Credential Manager for this target."
        : "Leave blank for an SSH-agent key or an unencrypted key. A saved passphrase requires an explicit key file.";
    public int TargetKindIndex { get => kind; set { if (SetProperty(ref kind, value)) NotifyLocation(); } }
    public int SourceIndex { get => source; set { if (SetProperty(ref source, value)) NotifyLocation(); } }
    public bool CloneRepository { get => SourceIndex == 1; set => SourceIndex = value ? 1 : 0; }
    public bool IsWsl => TargetKindIndex == 1;
    public bool IsSsh => TargetKindIndex == 2;
    public bool IsLocal => TargetKindIndex == 0;
    public bool IsRemote => !IsLocal;
    public string Host { get => host; set => SetProperty(ref host, value); }
    public string WslDistribution { get => distribution; set => SetProperty(ref distribution, value); }
    public string Path { get => path; set => SetProperty(ref path, value); }
    public string RepositoryUrl { get => repositoryUrl; set => SetProperty(ref repositoryUrl, value); }
    public string SelectedHost => IsWsl ? WslDistribution : Host;
    public string FolderLabel => CloneRepository ? "Clone destination" : "Workspace folder";
    public string FolderPlaceholder => IsLocal ? (CloneRepository ? @"C:\Projects\new-app" : @"C:\Projects\my-app")
        : CloneRepository ? "/home/elias/projects/new-app" : "/home/elias/projects/my-app";
    public string FolderHint => CloneRepository ? "The repository will be cloned directly into this new folder."
        : IsLocal ? "Choose the folder containing your project." : "Use the full Linux path inside this execution target.";
    public bool IsLocationComplete => !string.IsNullOrWhiteSpace(Path) && (!IsRemote || !string.IsNullOrWhiteSpace(SelectedHost))
        && (!CloneRepository || !string.IsNullOrWhiteSpace(RepositoryUrl)) && (!IsPasswordAuthentication || SshSecret.Length > 0)
        && (!IsKeyAuthentication || SshSecret.Length == 0 || SshKeyPath.Length > 0);
    public ExecutionTarget CreateTarget(Guid id, string name) => new() { Id = id, Name = name, Path = Path, Host = SelectedHost,
        Kind = TargetKindIndex switch { 1 => "wsl", 2 => "ssh", _ => "local" }, SshAuthentication = IsPasswordAuthentication ? "password" : "key",
        SshKeyPath = IsKeyAuthentication ? SshKeyPath.Trim() : "", HasSshSecret = IsSsh && SshSecret.Length > 0 };
    private void NotifyLocation()
    {
        foreach (var property in new[] { nameof(IsWsl), nameof(IsSsh), nameof(IsLocal), nameof(IsRemote), nameof(CloneRepository), nameof(FolderLabel), nameof(FolderPlaceholder), nameof(FolderHint), nameof(IsPasswordAuthentication), nameof(IsKeyAuthentication), nameof(SshSecretLabel), nameof(SshSecretHint) })
            OnPropertyChanged(property);
    }
}
