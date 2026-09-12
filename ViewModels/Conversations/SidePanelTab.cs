using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class SidePanelTab(string kind, string title, TerminalTabViewModel? terminal = null) : ObservableObject
{
    private string title = title;
    public string Kind { get; } = kind;
    public TerminalTabViewModel? Terminal { get; } = terminal;
    public string Title { get => title; set { if (Terminal is not null && !string.IsNullOrWhiteSpace(value)) SetProperty(ref title, value.Trim()); } }
}
