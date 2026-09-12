using System.Collections.ObjectModel;
using PiAgentGui.Services.Terminal;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Terminal;

public sealed class TerminalPanelViewModel(Func<string, ITerminalSession> createSession,
    Func<string, string, ITerminalSession>? createScriptSession = null) : ObservableObject, IAsyncDisposable
{
    private bool isOpen;
    private int sequence;
    private bool disposed;
    private readonly HashSet<Task> closing = [];
    private TerminalTabViewModel? selected;
    private Models.Projects.ExecutionTarget? target;
    public Models.Projects.ExecutionTarget? Target
    {
        get => target;
        set
        {
            if (target?.Id == value?.Id) return;
            target = value;
            var matching = Tabs.LastOrDefault(tab => tab.TargetId == value?.Id);
            if (matching is not null) Selected = matching;
            else IsOpen = false;
        }
    }
    public Func<Models.Projects.ExecutionTarget, string?, ITerminalSession>? CreateTargetSession { get; set; }
    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = [];
    public bool IsOpen { get => isOpen; private set => SetProperty(ref isOpen, value); }
    public TerminalTabViewModel? Selected { get => selected; set => SetProperty(ref selected, value); }

    public void Toggle(string directory)
    {
        if (disposed) return;
        if (IsOpen) IsOpen = false;
        else if (Target is null && Tabs.Count > 0) IsOpen = true;
        else if (Tabs.LastOrDefault(tab => tab.TargetId == Target?.Id && tab.Directory == directory) is { } existing) { Selected = existing; IsOpen = true; }
        else Add(directory);
    }

    public void Add(string directory)
    {
        if (disposed) return;
        var target = Target;
        var session = target is { IsLocal: false } ? CreateTargetSession?.Invoke(target with { Path = directory }, null)
            ?? throw new InvalidOperationException("Target terminals are unavailable.") : createSession(directory);
        var tab = new TerminalTabViewModel(directory, ++sequence, session, target is null ? null : $"{target.Label} · {sequence}") { TargetId = target?.Id };
        Tabs.Add(tab);
        Selected = tab;
        IsOpen = true;
    }

    public void Hide() => IsOpen = false;

    public void RunScript(Guid projectId, Guid scriptId, string name, string directory, string command, Models.Projects.ExecutionTarget? executionTarget = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = $"{projectId}:{executionTarget?.Id}:{scriptId}";
        var active = Tabs.FirstOrDefault(tab => tab.ScriptKey == key && !tab.IsFinished);
        if (active is null)
        {
            var session = executionTarget is { IsLocal: false } target ? CreateTargetSession?.Invoke(target with { Path = directory }, command)
                ?? throw new InvalidOperationException("Target script terminals are unavailable.") :
                createScriptSession?.Invoke(directory, command) ?? throw new InvalidOperationException("Script terminals are unavailable.");
            active = new(directory, ++sequence, session, executionTarget is null ? name : executionTarget.Label + " · " + name, key) { TargetId = executionTarget?.Id };
            Tabs.Add(active);
        }
        Selected = active;
        IsOpen = true;
    }

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
