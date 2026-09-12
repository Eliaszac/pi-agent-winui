using PiAgentGui.Models.Docker;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Docker;

public sealed class DockerContainerViewModel : ObservableObject
{
    private DockerContainer container;
    private string links = "All projects";
    private bool busy;
    private bool available = true;
    public DockerContainer Container => container;
    public string Name => container.Name;
    public string Image => container.Image;
    public string Status => container.Status;
    public string State => container.State;
    public string SourceName { get; }
    public string Links { get => links; set => SetProperty(ref links, value); }
    public bool Busy { get => busy; set { if (SetProperty(ref busy, value)) { OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); } } }
    public bool Available { get => available; set { if (SetProperty(ref available, value)) { OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); } } }
    public bool CanStart => Available && !Busy && container.CanStart;
    public bool CanStop => Available && !Busy && container.CanStop;
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }

    public DockerContainerViewModel(DockerContainer container, string sourceName, Func<DockerContainerViewModel, bool, Task> change, Action<Exception> error)
    {
        this.container = container;
        SourceName = sourceName;
        StartCommand = new(_ => change(this, true), error);
        StopCommand = new(_ => change(this, false), error);
    }

    public void Update(DockerContainer value)
    {
        if (container == value) return;
        container = value;
        foreach (var property in new[] { nameof(Name), nameof(Image), nameof(Status), nameof(State), nameof(CanStart), nameof(CanStop) }) OnPropertyChanged(property);
    }
}
