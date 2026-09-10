using PiAgentGui.Services.Terminal;

namespace PiAgentGui.ViewModels.Terminal;

public sealed class TerminalTabViewModel(string directory, int number, ITerminalSession session)
{
    public string Directory { get; } = directory;
    public string Title { get; } = $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))} · {number}";
    internal ITerminalSession Session { get; } = session;
}
