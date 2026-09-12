using PiAgentGui.Models.Docker;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Docker;

public sealed class DockerSourceViewModel(DockerSource source) : ObservableObject
{
    private string status = "Not checked";
    public DockerSource Source { get; } = source;
    public string Name => Source.Name;
    public string Status { get => status; set => SetProperty(ref status, value); }
}
