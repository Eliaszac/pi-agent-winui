using System.Collections.ObjectModel;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>In-memory tab order and selection owned by a single conversation.</summary>
public sealed class SidePanelState
{
    public ObservableCollection<SidePanelTab> Tabs { get; } = [];
    public SidePanelTab? Selected { get; set; }
    public bool IsOpen { get; set; }
    public SidePanelTab Open(string kind, string title, TerminalTabViewModel? terminal = null)
    {
        var tab = Tabs.FirstOrDefault(item => kind == "browser" ? false : kind == "terminal" ? terminal is not null && item.Terminal == terminal : item.Kind == kind);
        if (tab is null) { tab = new(kind, title, terminal); Tabs.Add(tab); }
        Selected = tab; IsOpen = true;
        return tab;
    }
    public void Close(SidePanelTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;
        Tabs.RemoveAt(index);
        if (Selected == tab) Selected = Tabs.Count > 0 ? Tabs[Math.Min(index, Tabs.Count - 1)] : null;
    }
}
