using System.Collections.ObjectModel;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Files;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Files;

public sealed class FileExplorerViewModel(IUiDispatcher dispatcher) : ObservableObject, IDisposable
{
    private ProjectFileSystem? files;
    private TargetFileReader? remoteFiles;
    private readonly CancellationTokenSource lifetime = new();
    private Guid? targetId;
    public bool IsReadOnlyTarget => remoteFiles is not null;
    public void SelectTarget(Models.Projects.ExecutionTarget? target)
    {
        if (targetId == target?.Id && Root == target?.Path) return;
        targetId = target?.Id;
        generation++; Items.Clear(); expanded = new(target is { IsLocal: false } ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase); Editing = null; Error = "";
        remoteFiles = null;
        SelectProject(null);
        if (target is { IsLocal: false })
        {
            generation++; Items.Clear(); expanded.Clear();
            remoteFiles = new(target, new Services.Projects.TargetCommandRunner());
            expanded.Add(target.Path);
            Error = "Target file browser is read-only. Use the agent or target terminal to edit files.";
            if (IsOpen) _ = RefreshAsync();
        }
        else SelectProject(target?.Path);
        OnPropertyChanged(nameof(IsReadOnlyTarget));
    }
    private FileSystemWatcher? watcher;
    private Timer? refreshTimer;
    private HashSet<string> expanded = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim toggles = new(1, 1);
    private int generation;
    private bool defaultsApplied;
    private bool disposed;
    private bool open;
    private bool busy;
    private string error = "";
    public ObservableCollection<ExplorerItem> Items { get; } = [];
    public bool IsOpen { get => open; set { if (SetProperty(ref open, value)) { if (value) _ = RefreshAsync(); } } }
    public bool IsBusy { get => busy; private set => SetProperty(ref busy, value); }
    public string Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string? Root => remoteFiles?.Root ?? files?.Root;
    public ExplorerItem? Editing { get; private set; }

    public void SelectProject(string? path)
    {
        if (string.Equals(path, Root, StringComparison.OrdinalIgnoreCase)) return;
        generation++;
        watcher?.Dispose(); watcher = null;
        refreshTimer?.Dispose(); refreshTimer = null;
        Items.Clear(); expanded.Clear(); Editing = null; Error = "";
        defaultsApplied = false;
        files = path is null ? null : new ProjectFileSystem(path, RecycleBin.Delete);
        if (files is null) return;
        expanded.Add(files.Root);
        try
        {
            refreshTimer = new Timer(_ => dispatcher.Post(() => { if (!disposed && IsOpen && Editing is null) _ = RefreshAsync(); }), null, Timeout.Infinite, Timeout.Infinite);
            watcher = new FileSystemWatcher(files.Root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
            watcher.Created += OnFilesChanged; watcher.Deleted += OnFilesChanged; watcher.Renamed += OnFilesChanged;
            watcher.Error += (_, _) => dispatcher.Post(() => { Error = "File watching was interrupted. Use Refresh to reload the folder."; });
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { Report(exception); }
        if (IsOpen) _ = RefreshAsync();
    }

    private void OnFilesChanged(object sender, FileSystemEventArgs args)
    {
        try { refreshTimer?.Change(250, Timeout.Infinite); } catch (ObjectDisposedException) { }
    }

    public async Task RefreshAsync()
    {
        if (remoteFiles is { } remote)
        {
            var version = ++generation;
            var remoteOpened = expanded.ToHashSet(StringComparer.Ordinal);
            try
            {
                var rows = new List<ExplorerItem> { new(remote.Root, true, 0, true) };
                await ReadRemoteBranchAsync(remote, remote.Root, 1, remoteOpened, rows);
                if (version != generation || disposed) return;
                ObservableCollectionSynchronizer.Synchronize(Items, rows);
                Error = "Target file browser is read-only. Use the agent or target terminal to edit files.";
            }
            catch (Exception exception) { if (version == generation && !disposed) Report(exception); }
            return;
        }
        var service = files;
        if (service is null || disposed || Editing is not null) return;
        var revision = ++generation;
        var opened = expanded.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var applyDefaults = !defaultsApplied;
        try
        {
            var rows = await Task.Run(() =>
            {
                if (applyDefaults) ExplorerSnapshot.ExpandSmallRootChildren(service, opened);
                return ExplorerSnapshot.Read(service, opened);
            });
            if (revision != generation || disposed || Editing is not null) return;
            if (applyDefaults)
            {
                expanded.UnionWith(opened);
                defaultsApplied = true;
            }
            var old = Items.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
            var reused = rows.Select(row => old.TryGetValue(row.Path, out var previous)
                && previous.Depth == row.Depth && previous.IsExpanded == row.IsExpanded && previous.IsLinked == row.IsLinked
                && previous.IsDirectory == row.IsDirectory && previous.Path == row.Path ? previous : row).ToArray();
            ObservableCollectionSynchronizer.Synchronize(Items, reused);
            Error = "";
        }
        catch (Exception exception) { if (revision == generation && !disposed) Report(exception); }
    }

    public async Task ToggleAsync(ExplorerItem item)
    {
        if (remoteFiles is not null)
        {
            if (!item.CanExpand || item.IsRoot) return;
            if (!expanded.Remove(item.Path)) expanded.Add(item.Path);
            await RefreshAsync(); return;
        }
        var originalService = files;
        await toggles.WaitAsync();
        try
        {
            if (originalService != files || disposed) return;
            var current = Items.FirstOrDefault(row => row.Path == item.Path);
            if (current is not null) await ToggleCoreAsync(current);
        }
        finally { toggles.Release(); }
    }

    private async Task ToggleCoreAsync(ExplorerItem item)
    {
        if (!item.CanExpand || Editing is not null || IsBusy || files is not { } service) return;
        var revision = ++generation;
        if (expanded.Remove(item.Path))
        {
            var index = Items.IndexOf(item);
            var end = index + 1;
            while (end < Items.Count && Items[end].Depth > item.Depth) end++;
            for (var current = end - 1; current > index; current--) Items.RemoveAt(current);
            item.IsExpanded = false;
            return;
        }
        expanded.Add(item.Path);
        var opened = expanded.ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var children = await Task.Run(() => ExplorerSnapshot.ReadBranch(service, item.Path, item.Depth, opened));
            if (revision != generation || disposed || Editing is not null) return;
            var index = Items.IndexOf(item);
            if (index < 0) return;
            item.IsExpanded = true;
            for (var offset = 0; offset < children.Count; offset++) Items.Insert(index + offset + 1, children[offset]);
            Error = "";
        }
        catch (Exception exception) { if (revision == generation && !disposed) { expanded.Remove(item.Path); Report(exception); } }
    }

    public async Task BeginCreateAsync(ExplorerItem parent, bool folder)
    {
        if (IsBusy || Editing is not null || files is null) return;
        var service = files;
        var directory = parent.IsDirectory ? parent.Path : Path.GetDirectoryName(parent.Path)!;
        expanded.Add(directory);
        await RefreshAsync();
        if (files != service || disposed || Editing is not null) return;
        var row = Items.FirstOrDefault(item => item.Path == directory);
        if (row is null) return;
        Editing = new ExplorerItem(Path.Combine(directory, ".pi-explorer-new-" + Guid.NewGuid()), folder, row.Depth + 1, false)
            { CreateParent = directory, IsEditing = true, Draft = folder ? "New folder" : "New file" };
        Items.Insert(Items.IndexOf(row) + 1, Editing);
    }

    public void BeginRename(ExplorerItem item)
    {
        if (IsReadOnlyTarget) return;
        if (item.IsRoot || item.IsLinked || IsBusy || Editing is not null) return;
        Editing = item; item.Draft = item.Name; item.IsEditing = true;
    }

    public async Task CommitEditAsync()
    {
        if (Editing is not { } item || files is not { } service || IsBusy) return;
        var name = item.Draft;
        await MutateAsync(() => item.CreateParent is { } parent ? service.Create(parent, name, item.IsDirectory) : service.Rename(item.Path, name), item.CreateParent is null ? item.Path : null);
    }

    public void CancelEdit()
    {
        if (IsBusy) return;
        if (Editing is { } item) { item.IsEditing = false; if (item.CreateParent is not null) Items.Remove(item); }
        Editing = null;
        _ = RefreshAsync();
    }

    public async Task MoveAsync(string source, ExplorerItem target)
    {
        if (files is not { } service || !target.IsDirectory || Editing is not null) return;
        expanded.Add(target.Path);
        await MutateAsync(() => service.Move(source, target.Path), source);
    }

    public async Task DeleteAsync(ExplorerItem item)
    {
        if (files is not { } service || item.IsRoot || Editing is not null) return;
        await MutateAsync(() => { service.Delete(item.Path); return ""; });
    }

    private async Task MutateAsync(Func<string> operation, string? source = null)
    {
        if (IsBusy || disposed) return;
        IsBusy = true; Error = "";
        var service = files;
        try
        {
            var destination = await Task.Run(operation);
            if (service != files || disposed) return;
            if (source is not null && destination.Length > 0)
                foreach (var path in expanded.Where(path => path.Equals(source, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToArray())
                { expanded.Remove(path); expanded.Add(destination + path[source.Length..]); }
            if (Editing is { } item) item.IsEditing = false;
            Editing = null;
            await RefreshAsync();
        }
        catch (Exception exception) { if (service == files && !disposed) Report(exception); }
        finally { IsBusy = false; }
    }

    public void Report(Exception exception) => Error = exception is OperationCanceledException ? "Operation cancelled." : exception.Message;
    private async Task ReadRemoteBranchAsync(TargetFileReader reader, string path, int depth, IReadOnlySet<string> opened, List<ExplorerItem> rows)
    {
        if (depth > 32 || rows.Count > 20000) throw new IOException("Collapse some target folders before expanding more.");
        foreach (var item in await reader.ListAsync(path, depth, lifetime.Token))
        {
            item.IsExpanded = item.CanExpand && opened.Contains(item.Path);
            rows.Add(item);
            if (item.IsExpanded) await ReadRemoteBranchAsync(reader, item.Path, depth + 1, opened, rows);
        }
    }
    public void Dispose() { disposed = true; generation++; lifetime.Cancel(); watcher?.Dispose(); refreshTimer?.Dispose(); }
}
