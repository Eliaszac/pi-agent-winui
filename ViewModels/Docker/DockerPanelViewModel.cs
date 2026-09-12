using System.Collections.ObjectModel;
using PiAgentGui.Models.Docker;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Docker;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Docker;

/// <summary>UI-thread-owned Docker state shared by extension management and conversation panels.</summary>
public sealed class DockerPanelViewModel(DockerPreferenceStore store, IDockerClient client, DockerSourceDiscovery discovery) : ObservableObject, IDisposable
{
    private DockerPreferences settings = DockerPreferences.Empty;
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? refresh;
    private bool loaded;
    private bool isOpen;
    private bool busy;
    private string message = "";
    private Guid? projectId;
    public bool Enabled => settings.Enabled;
    public bool IsOpen { get => isOpen; set => SetProperty(ref isOpen, value && Enabled); }
    public bool Busy
    {
        get => busy;
        private set
        {
            if (!SetProperty(ref busy, value)) return;
            foreach (var row in Containers) row.Available = !value;
            OnPropertyChanged(nameof(CanManage));
        }
    }
    public bool CanManage => loaded && !Busy;
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public ObservableCollection<DockerSourceViewModel> Sources { get; } = [];
    public ObservableCollection<DockerContainerViewModel> Containers { get; } = [];
    public ObservableCollection<DockerContainerViewModel> VisibleContainers { get; } = [];
    public IReadOnlyList<DockerContainerViewModel> LinkContainers => Containers.DistinctBy(row => row.Container.Id).ToArray();
    public IReadOnlyList<Project> Projects { get; private set; } = [];
    public IReadOnlyList<DockerSource> SshCandidates { get; private set; } = [];
    public AsyncRelayCommand RefreshCommand => field ??= new(_ => RefreshAsync(), ReportError);
    public AsyncRelayCommand DetectCommand => field ??= new(_ => DetectAsync(), ReportError);

    public async Task InitializeAsync()
    {
        try
        {
            settings = await store.ReadAsync();
            loaded = true;
            OnPropertyChanged(nameof(CanManage));
            foreach (var source in settings.Sources) Sources.Add(new(source));
            OnPropertyChanged(nameof(Enabled));
        }
        catch (Exception error) { ReportError(error); }
    }

    public void ReportError(Exception error) => Message = error.Message;

    public async Task SetEnabledAsync(bool value)
    {
        await SaveAsync(current => current with { Enabled = value });
        if (!value)
        {
            refresh?.Cancel();
            IsOpen = false;
            Containers.Clear(); VisibleContainers.Clear();
            OnPropertyChanged(nameof(LinkContainers));
        }
        OnPropertyChanged(nameof(Enabled));
        if (value && Sources.Count == 0) await DetectCommand.ExecuteAsync();
    }

    public async Task LoadManagementAsync()
    {
        try
        {
            Projects = await discovery.ProjectsAsync();
            var projectIds = Projects.Select(project => project.Id).ToHashSet();
            if (settings.Links.Any(pair => pair.Value.Any(id => !projectIds.Contains(id))))
                await SaveAsync(current => current with { Links = current.Links
                    .Select(pair => new KeyValuePair<string, Guid[]>(pair.Key, pair.Value.Where(projectIds.Contains).ToArray()))
                    .Where(pair => pair.Value.Length > 0).ToDictionary() });
            SshCandidates = (await discovery.SshAsync()).Where(candidate => !settings.Sources.Any(source => DockerSourceDiscovery.Same(candidate, source))).ToArray();
            OnPropertyChanged(nameof(Projects)); OnPropertyChanged(nameof(SshCandidates));
            ApplyFilter();
        }
        catch (Exception error) { ReportError(error); }
        await RefreshAsync();
    }

    private async Task DetectAsync()
    {
        if (!Enabled) { Message = "Enable Docker on its extension card to detect sources."; return; }
        if (Busy) return;
        Busy = true;
        Message = "Checking Windows and WSL for Docker…";
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        refresh = request;
        try
        {
            var found = await discovery.DetectAsync(request.Token);
            if (!Enabled) return;
            foreach (var source in found)
            {
                request.Token.ThrowIfCancellationRequested();
                if (!settings.Sources.Any(saved => DockerSourceDiscovery.Same(saved, source))) await AddSourceAsync(source, refreshAfter: false);
            }
            Message = found.Count == 0 ? "No Docker CLI found in Windows or WSL. Install Docker, then detect again, or add a saved SSH target." : "Detection complete. Docker uses each source’s current context.";
            if (discovery.Warning is { } warning) Message += " " + warning;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        finally { refresh = null; Busy = false; }
        await RefreshAsync(clearMessage: false);
    }

    public async Task AddSourceAsync(DockerSource source, bool refreshAfter = true)
    {
        if (settings.Sources.Any(saved => DockerSourceDiscovery.Same(saved, source))) return;
        await SaveAsync(current => current.Sources.Any(saved => DockerSourceDiscovery.Same(saved, source)) ? current
            : current with { Sources = current.Sources.Append(source).ToArray() });
        foreach (var saved in settings.Sources)
            if (!Sources.Any(row => row.Source.Id == saved.Id)) Sources.Add(new(saved));
        if (refreshAfter) await LoadManagementAsync();
    }

    public async Task RemoveSourceAsync(DockerSource source)
    {
        refresh?.Cancel();
        await SaveAsync(current => current with
        {
            Sources = current.Sources.Where(saved => saved.Id != source.Id).ToArray(),
            // A Windows and WSL source can share an engine. Keep links until the remaining sources have been checked.
            Links = current.Sources.All(saved => saved.Id == source.Id) ? new Dictionary<string, Guid[]>() : current.Links
        });
        var row = Sources.FirstOrDefault(row => row.Source.Id == source.Id);
        if (row is not null) Sources.Remove(row);
        foreach (var container in Containers.Where(row => row.Container.SourceId == source.Id).ToArray()) Containers.Remove(container);
        ApplyFilter();
        await LoadManagementAsync();
    }

    public Guid[] LinkedProjects(DockerContainer container) => settings.Links.TryGetValue(container.Id, out var ids) ? ids : [];

    public async Task LinkAsync(DockerContainer container, IEnumerable<Guid> projects)
    {
        var ids = projects.Distinct().ToArray();
        await SaveAsync(current =>
        {
            var links = current.Links.ToDictionary();
            if (ids.Length == 0) links.Remove(container.Id); else links[container.Id] = ids;
            return current with { Links = links };
        });
        ApplyFilter();
    }

    public void SelectProject(Guid? id) { projectId = id; ApplyFilter(); }

    public async Task RefreshAsync(bool clearMessage = true)
    {
        if (!Enabled || Busy || lifetime.IsCancellationRequested) return;
        Busy = true;
        if (clearMessage) Message = "";
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        refresh = request;
        var inventories = new Dictionary<Guid, HashSet<string>>();
        try
        {
            await Task.WhenAll(Sources.ToArray().Select(async source =>
            {
                try
                {
                    var rows = await client.ListAsync(source.Source, request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    inventories[source.Source.Id] = rows.Select(row => row.Id).ToHashSet();
                    source.Status = rows.Count == 0 ? "Connected · no containers" : $"Connected · {rows.Count} containers";
                    foreach (var stale in Containers.Where(row => row.Container.SourceId == source.Source.Id && !rows.Any(next => next.Id == row.Container.Id)).ToArray()) Containers.Remove(stale);
                    foreach (var container in rows)
                    {
                        var row = Containers.FirstOrDefault(row => row.Container.Key == container.Key);
                        if (row is null) Containers.Add(new(container, source.Name, SetRunningAsync, ReportError) { Available = false });
                        else row.Update(container);
                    }
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested) { }
                catch (Exception error)
                {
                    source.Status = "Unavailable · " + error.Message;
                    foreach (var row in Containers.Where(row => row.Container.SourceId == source.Source.Id).ToArray()) Containers.Remove(row);
                }
            }));
            // Only an authoritative inventory of every source can prove that a shared container is gone.
            var liveIds = inventories.Values.SelectMany(ids => ids).ToHashSet();
            if (!request.IsCancellationRequested && settings.Sources.All(source => inventories.ContainsKey(source.Id)) && settings.Links.Keys.Any(id => !liveIds.Contains(id)))
                await SaveAsync(current => current.Sources.All(source => inventories.ContainsKey(source.Id))
                    ? current with { Links = current.Links.Where(pair => liveIds.Contains(pair.Key)).ToDictionary() } : current);
            ApplyFilter();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) { ReportError(error); }
        finally { refresh = null; Busy = false; }
    }

    private async Task SetRunningAsync(DockerContainerViewModel row, bool running)
    {
        if (!Enabled || Busy || row.Busy || (running ? !row.CanStart : !row.CanStop)) return;
        var source = settings.Sources.SingleOrDefault(source => source.Id == row.Container.SourceId);
        if (source is null || !Containers.Contains(row)) return;
        Busy = true; row.Busy = true; Message = "";
        try { await client.SetRunningAsync(source, row.Container.Id, running, lifetime.Token); }
        catch (Exception error) { Message = "Could not " + (running ? "start" : "stop") + " “" + row.Name + "”: " + error.Message; }
        finally { Busy = false; row.Busy = false; }
        await RefreshAsync(clearMessage: false);
    }

    private void ApplyFilter()
    {
        foreach (var row in Containers)
        {
            var ids = LinkedProjects(row.Container);
            row.Links = ids.Length == 0 ? "All projects" : string.Join(", ", ids.Select(id => Projects.FirstOrDefault(project => project.Id == id)?.Name ?? "Unavailable project"));
        }
        var visible = Containers.Where(row => DockerOutput.Visible(row.Container, projectId, settings.Links))
            .DistinctBy(row => row.Container.Id)
            .OrderBy(row => row.SourceName).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var stale in VisibleContainers.Except(visible).ToArray()) VisibleContainers.Remove(stale);
        for (var index = 0; index < visible.Length; index++)
        {
            var existing = VisibleContainers.IndexOf(visible[index]);
            if (existing < 0) VisibleContainers.Insert(index, visible[index]);
            else if (existing != index) VisibleContainers.Move(existing, index);
        }
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(LinkContainers));
    }

    public string EmptyText => Sources.Count == 0 ? "No sources yet. Open Manage to detect Docker or add an SSH target."
        : VisibleContainers.Count == 0 ? "No containers for this project. Check source status or project links in Manage." : "";

    private async Task SaveAsync(Func<DockerPreferences, DockerPreferences> update)
    {
        if (!loaded) throw new IOException("Docker settings could not be loaded. Fix the saved configuration before making changes.");
        await writes.WaitAsync(lifetime.Token);
        try
        {
            var next = update(settings);
            await store.WriteAsync(next);
            settings = next;
        }
        finally { writes.Release(); }
    }

    public void Dispose() { lifetime.Cancel(); refresh?.Cancel(); }
}
