namespace PiAgentGui.Models.Pi;

public sealed record AgentProcess(ProcessIdentity Identity, int ParentId, string Name) : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void RefreshElapsed() => PropertyChanged?.Invoke(this, new(nameof(Details)));
    public string Details => $"PID {Identity.Id} · {Elapsed}";
    private string Elapsed
    {
        get
        {
            var elapsed = DateTime.UtcNow - Identity.StartedUtc;
            return elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s" : $"{Math.Max(0, (int)elapsed.TotalSeconds)}s";
        }
    }
}
