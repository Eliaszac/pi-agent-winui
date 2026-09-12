using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Projects;

/// <summary>Flattens expanded project groups without duplicating conversation state.</summary>
public sealed class SidebarRows(IUiDispatcher dispatcher) : IDisposable
{
    private ObservableCollection<ProjectItemViewModel>? projects;
    private readonly Dictionary<ProjectItemViewModel, (SidebarGroupRow Empty, SidebarGroupRow Settled)> observed = [];
    private bool scheduled, disposed;
    public ObservableCollection<object> Rows { get; } = [];
    public void SetProjects(ObservableCollection<ProjectItemViewModel> source)
    {
        if (ReferenceEquals(projects, source)) return;
        if (projects is not null) projects.CollectionChanged -= Changed;
        Detach(); projects = source; projects.CollectionChanged += Changed; Queue();
    }
    private void Changed(object? sender, NotifyCollectionChangedEventArgs args) => Queue();
    private void ProjectChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ProjectItemViewModel.IsExpanded) or nameof(ProjectItemViewModel.IsSettledExpanded)) Queue();
    }
    private void Queue()
    {
        if (scheduled || disposed) return;
        scheduled = true;
        if (!dispatcher.Post(() => { scheduled = false; if (!disposed) Rebuild(); })) scheduled = false;
    }
    private void Rebuild()
    {
        var current = projects?.ToHashSet() ?? [];
        foreach (var project in observed.Keys.Where(project => !current.Contains(project)).ToArray()) Unsubscribe(project);
        foreach (var project in current.Where(project => !observed.ContainsKey(project)))
        {
            observed[project] = (new(project, true), new(project, false));
            project.PropertyChanged += ProjectChanged;
            project.ActiveConversations.CollectionChanged += Changed;
            project.SettledConversations.CollectionChanged += Changed;
        }
        var rows = new List<object>();
        foreach (var project in projects ?? [])
        {
            rows.Add(project);
            if (!project.IsExpanded) continue;
            rows.AddRange(project.ActiveConversations);
            if (project.HasNoConversations) rows.Add(observed[project].Empty);
            if (!project.HasSettledConversations) continue;
            rows.Add(observed[project].Settled);
            if (project.IsSettledExpanded) rows.AddRange(project.SettledConversations);
        }
        ObservableCollectionSynchronizer.Synchronize(Rows, rows);
    }
    private void Unsubscribe(ProjectItemViewModel project)
    {
        project.PropertyChanged -= ProjectChanged;
        project.ActiveConversations.CollectionChanged -= Changed;
        project.SettledConversations.CollectionChanged -= Changed;
        observed.Remove(project);
    }
    private void Detach() { foreach (var project in observed.Keys.ToArray()) Unsubscribe(project); }
    public void Dispose() { disposed = true; if (projects is not null) projects.CollectionChanged -= Changed; Detach(); }
}
