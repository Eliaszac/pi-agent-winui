using PiAgentGui.Services.Terminal;

namespace PiAgentGui.ViewModels.Terminal;

public sealed class TerminalTabViewModel
{
    private volatile bool finished;
    public string Directory { get; }
    public string Title { get; }
    public string? ScriptKey { get; }
    public bool IsFinished => finished;
    internal ITerminalSession Session { get; }
    public TerminalTabViewModel(string directory, int number, ITerminalSession session, string? title = null, string? scriptKey = null)
    {
        Directory = directory;
        Title = title ?? $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))} · {number}";
        ScriptKey = scriptKey;
        Session = session;
        session.Exited += () => finished = true;
    }
}
