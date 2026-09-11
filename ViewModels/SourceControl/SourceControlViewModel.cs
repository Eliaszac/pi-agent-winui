using System.Collections.ObjectModel;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.SourceControl;

public sealed class SourceControlViewModel(GitRepositoryService service) : ObservableObject, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, string> drafts = new(StringComparer.OrdinalIgnoreCase);
    private string? directory;
    private GitSnapshot? state;
    private int revision;
    private bool open;
    private bool busy;
    private bool disposed;
    private bool loaded;
    private bool readFailed;
    private string message = "";
    private string error = "";
    private string notice = "";
    private string? remote;
    public ObservableCollection<GitChange> Staged { get; } = [];
    public ObservableCollection<GitChange> Changes { get; } = [];
    public ObservableCollection<GitChange> Conflicts { get; } = [];
    public ObservableCollection<GitBranch> Branches { get; } = [];
    public ObservableCollection<string> Remotes { get; } = [];
    public bool IsOpen { get => open; set => SetProperty(ref open, value); }
    public bool IsBusy { get => busy; private set { if (SetProperty(ref busy, value)) NotifyState(); } }
    public string CommitMessage { get => message; set { if (SetProperty(ref message, value)) { if (directory is not null) drafts[directory] = value; OnPropertyChanged(nameof(CanCommit)); } } }
    public string Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string Notice { get => notice; private set { if (SetProperty(ref notice, value)) OnPropertyChanged(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;
    public string? SelectedRemote { get => remote; set => SetProperty(ref remote, value); }
    public bool HasRepository => state is not null;
    public string? RepositoryRoot => state?.Root;
    public bool ShowEmpty => state is null;
    public string EmptyMessage => !loaded ? "Checking repository…" : HasError ? "Repository information is unavailable." : "This project is not inside a Git repository.";
    public bool CanAct => state is not null && !IsBusy && !disposed;
    public bool CanCommit => CanAct && !state!.Detached && !state.Conflicted && Staged.Count > 0 && !string.IsNullOrWhiteSpace(CommitMessage);
    public bool CanPush => CanAct && !state!.Detached && state.HasHead && Remotes.Count > 0;
    public bool CanStageAll => CanAct && !HasConflicts && Changes.Count > 0;
    public bool CanUnstageAll => CanAct && Staged.Count > 0;
    public string BranchName => state?.BranchName ?? "No repository";
    public string StagedLabel => $"Staged changes ({Staged.Count})";
    public string ChangesLabel => $"Changes ({Changes.Count})";
    public string ConflictsLabel => $"Merge conflicts ({Conflicts.Count})";
    public string ConflictMessage => state?.Conflicted == true ? "Resolve the files below before committing. Applying a resolution stages that file." : "";
    public bool HasConflicts => state?.Conflicted == true;

    public void SelectProject(string? path)
    {
        if (string.Equals(directory, path, StringComparison.OrdinalIgnoreCase)) return;
        directory = path; revision++; state = null; loaded = false;
        Staged.Clear(); Changes.Clear(); Conflicts.Clear(); Branches.Clear(); Remotes.Clear();
        SelectedRemote = null; Error = ""; Notice = "";
        CommitMessage = path is not null ? drafts.GetValueOrDefault(path, "") : "";
        NotifyState();
    }

    public async Task RefreshAsync()
    {
        if (disposed || IsBusy || directory is null || !await gate.WaitAsync(0)) return;
        try { await ReadCoreAsync(directory, revision); }
        finally { gate.Release(); }
    }

    private async Task ReadCoreAsync(string path, int version)
    {
        try
        {
            var result = await Task.Run(() => service.ReadAsync(path, lifetime.Token));
            if (disposed || version != revision) return;
            state = result; loaded = true;
            if (readFailed) { Error = ""; readFailed = false; }
            ObservableCollectionSynchronizer.Synchronize(Staged, result?.Changes.Where(item => item.Staged).ToArray() ?? []);
            ObservableCollectionSynchronizer.Synchronize(Changes, result?.Changes.Where(item => !item.Staged && item.Status != 'U').ToArray() ?? []);
            ObservableCollectionSynchronizer.Synchronize(Conflicts, result?.Changes.Where(item => item.Status == 'U').ToArray() ?? []);
            ObservableCollectionSynchronizer.Synchronize(Branches, result?.Branches ?? []);
            ObservableCollectionSynchronizer.Synchronize(Remotes, result?.Remotes ?? []);
            if (SelectedRemote is null || !Remotes.Contains(SelectedRemote)) SelectedRemote = Remotes.Contains("origin") ? "origin" : Remotes.FirstOrDefault();
            NotifyState();
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception exception) { if (!disposed && version == revision) { Error = GitErrorMessage.Format(exception.Message); readFailed = true; loaded = true; NotifyState(); } }
    }

    public Task StageAsync(GitChange? change) => RunAsync((snapshot, token) => service.StageAsync(snapshot, change, token), "Changes staged.");
    public async Task<MergeConflict?> ReadConflictAsync(GitChange change)
    {
        if (!CanAct || state is not { } snapshot) return null;
        var version = revision; IsBusy = true; Error = "";
        await gate.WaitAsync();
        try
        {
            if (disposed || version != revision) return null;
            var conflict = await Task.Run(() => service.ReadConflictAsync(snapshot, change, lifetime.Token));
            return !disposed && version == revision ? conflict : null;
        }
        catch (Exception exception) { if (!disposed && version == revision) Error = GitErrorMessage.Format(exception.Message); return null; }
        finally { gate.Release(); IsBusy = false; }
    }
    public async Task<bool> ApplyConflictAsync(MergeConflict conflict, string result, bool delete)
    {
        var applied = false;
        await RunAsync(async (snapshot, token) =>
        {
            if (snapshot.Root != conflict.Root) throw new InvalidOperationException("The selected repository changed. Reopen the resolver.");
            await Task.Run(() => service.ApplyConflictAsync(conflict, result, delete, token));
            applied = true;
        }, "Conflict resolved and staged. Review remaining files before committing.");
        return applied;
    }
    public async Task<Models.Conversations.FileDiffContent?> ReadDiffAsync(GitChange change)
    {
        if (!CanAct || state is not { } snapshot) return null;
        var version = revision; IsBusy = true; Error = "";
        await gate.WaitAsync();
        try
        {
            if (disposed || version != revision) return null;
            var content = await Task.Run(() => service.ReadDiffAsync(snapshot, change, lifetime.Token));
            return version == revision && !disposed ? content : null;
        }
        catch (OperationCanceledException) when (disposed) { return null; }
        catch (Exception exception) { if (!disposed && version == revision) Error = GitErrorMessage.Format(exception.Message); return null; }
        finally { gate.Release(); IsBusy = false; }
    }
    public Task UnstageAsync(GitChange? change) => RunAsync((snapshot, token) => service.UnstageAsync(snapshot, change, token), "Changes unstaged.");
    public Task RevertAsync(GitChange change, string confirmedRoot) => RunAsync((snapshot, token) =>
    {
        if (!string.Equals(snapshot.Root, confirmedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected repository changed. Reopen the file menu to revert.");
        return service.RevertAsync(snapshot, change, token);
    }, "File changes reverted.");
    public Task CommitAsync()
    {
        var text = CommitMessage;
        return RunAsync((snapshot, token) => service.CommitAsync(snapshot, text, token), "Commit created.", () => { if (CommitMessage == text) CommitMessage = ""; });
    }
    public Task PushAsync()
    {
        var destination = SelectedRemote;
        return RunAsync((snapshot, token) => service.PushAsync(snapshot, destination, token), "Push completed.");
    }
    public Task FetchAsync(GitBranch? branch = null) => RunAsync((snapshot, token) => service.FetchAsync(snapshot, branch, token), "Fetch completed.");
    public Task PullAsync() => RunAsync(service.PullAsync, "Pull completed.");
    public Task CheckoutAsync(GitBranch branch, string? name = null) => RunAsync((snapshot, token) => service.CheckoutAsync(snapshot, branch, name, token), "Branch checked out.");
    public Task MergeAsync(GitBranch branch) => RunAsync((snapshot, token) => service.MergeAsync(snapshot, branch, token), "Merge completed.");
    public void ReportError(string text) => Error = text;

    private async Task RunAsync(Func<GitSnapshot, CancellationToken, Task> operation, string success, Action? onSuccess = null)
    {
        if (!CanAct || state is not { } snapshot || directory is not { } path) return;
        var version = revision;
        IsBusy = true; Error = ""; Notice = "";
        await gate.WaitAsync();
        try
        {
            if (disposed || version != revision) return;
            await operation(snapshot, lifetime.Token);
            if (version == revision && !disposed) { onSuccess?.Invoke(); Notice = success; }
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception exception) { if (!disposed && version == revision) Error = GitErrorMessage.Format(exception.Message); }
        finally
        {
            if (!disposed && version == revision) await ReadCoreAsync(path, version);
            gate.Release(); IsBusy = false;
        }
    }

    private void NotifyState()
    {
        foreach (var property in new[] { nameof(HasRepository), nameof(ShowEmpty), nameof(EmptyMessage), nameof(CanAct), nameof(CanCommit), nameof(CanPush), nameof(CanStageAll), nameof(CanUnstageAll), nameof(BranchName), nameof(StagedLabel), nameof(ChangesLabel), nameof(HasConflicts), nameof(ConflictMessage), nameof(ConflictsLabel) }) OnPropertyChanged(property);
    }
    public void Dispose() { disposed = true; revision++; lifetime.Cancel(); }
}
