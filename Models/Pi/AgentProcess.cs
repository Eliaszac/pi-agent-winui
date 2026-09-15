namespace PiAgentGui.Models.Pi;

public sealed record AgentProcess(ProcessIdentity Identity, int ParentId, string Name) : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public Utilities.ProcessPresentation Presentation { get; } = Utilities.ProcessPresentation.ForExecutable(Name);
    public string ProcessId => $"Process ID: {Identity.Id}";
    public string ParentProcessId => $"Parent process ID: {ParentId}";
    public string Started => $"Started: {Identity.StartedUtc.ToLocalTime():g}";
    public string StopLabel => $"Stop {Presentation.Title}";
    private bool expanded;
    public bool IsExpanded
    {
        get => expanded;
        set
        {
            if (expanded == value) return;
            expanded = value;
            PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
        }
    }
    public void RefreshElapsed() => PropertyChanged?.Invoke(this, new(nameof(Details)));
    public string Details => $"Running for {Elapsed}";
    private string Elapsed
    {
        get
        {
            var elapsed = DateTime.UtcNow - Identity.StartedUtc;
            return elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s" : $"{Math.Max(0, (int)elapsed.TotalSeconds)}s";
        }
    }
}
