using System.Collections.ObjectModel;
using PiAgentGui.Services.Terminal;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Terminal;

public sealed class TerminalPanelViewModel(Func<string, ITerminalSession> createSession) : ObservableObject, IAsyncDisposable
{
    private bool isOpen;
    private int sequence;
    private bool disposed;
    private readonly HashSet<Task> closing = [];
    private TerminalTabViewModel? selected;
    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = [];
    public bool IsOpen { get => isOpen; private set => SetProperty(ref isOpen, value); }
    public TerminalTabViewModel? Selected { get => selected; set => SetProperty(ref selected, value); }

    public void Toggle(string directory)
    {
        if (disposed) return;
        if (IsOpen) IsOpen = false;
        else if (Tabs.Count == 0) Add(directory);
        else IsOpen = true;
    }

    public void Add(string directory)
    {
        if (disposed) return;
        var tab = new TerminalTabViewModel(directory, ++sequence, createSession(directory));
        Tabs.Add(tab);
        Selected = tab;
        IsOpen = true;
    }

    public void Hide() => IsOpen = false;

    public async Task CloseAsync(TerminalTabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;
        Tabs.Remove(tab);
        if (Selected == tab) Selected = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        if (Tabs.Count == 0) IsOpen = false;
        var cleanup = tab.Session.DisposeAsync().AsTask();
        closing.Add(cleanup);
        try { await cleanup; }
        finally { closing.Remove(cleanup); }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await Task.WhenAll(Tabs.Select(tab => tab.Session.DisposeAsync().AsTask()).Concat(closing).ToArray());
    }
}
