using PiAgentGui.Models.GitHub;
using PiAgentGui.Services.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.GitHub;

public sealed class GitHubViewModel(GitHubAuthentication authentication, GitHubApi api, IGitBranchReader git) : ObservableObject
{
    private GitHubToken? token;
    private string? selectedPath;
    private bool refreshing;
    private bool connecting;
    private bool selectedIsRepository;
    private int selectionRevision;
    private string error = "";
    private readonly Dictionary<string, GitHubPullRequest?> pulls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GitHubBranch?> branches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> checkedAt = new(StringComparer.OrdinalIgnoreCase);
    public bool IsConnected => token is not null;
    public bool IsConnecting => connecting;
    public GitHubPullRequest? SelectedPullRequest => selectedPath is not null ? pulls.GetValueOrDefault(selectedPath) : null;
    public string HeaderLabel => connecting ? "Connecting…" : IsConnected ? "Open PR" : "Connect to GitHub";
    public string HeaderTooltip => SelectedPullRequest is { } pr ? $"Open PR #{pr.Number}: {pr.Title}" : "Connect to GitHub";
    public bool ShowHeaderButton => selectedIsRepository && (!IsConnected || SelectedPullRequest is not null);
    public string Error { get => error; private set { SetProperty(ref error, value); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public event Action<string, GitHubPullRequest?>? PullRequestChanged;

    public void Initialize()
    {
        try { token = authentication.Load(); }
        catch (Exception) { Error = "Couldn't read the saved GitHub login from Windows Credential Locker."; }
        Notify();
    }

    public void Select(string? path)
    {
        if (!string.Equals(selectedPath, path, StringComparison.OrdinalIgnoreCase))
        {
            ++selectionRevision;
            selectedIsRepository = false;
        }
        selectedPath = path;
        Error = "";
        Notify();
    }
    public void ReportError(string message) => Error = message;
    public void Disconnect()
    {
        authentication.Disconnect();
        token = null;
        Clear();
        Notify();
    }

    public async Task ConnectAsync(Action<string> showCode, CancellationToken cancellationToken)
    {
        if (connecting) return;
        connecting = true;
        Error = "";
        Notify();
        try
        {
            var code = await authentication.BeginAsync(cancellationToken);
            showCode(code.UserCode);
            token = await authentication.CompleteAsync(code, cancellationToken);
            checkedAt.Clear();
        }
        finally { connecting = false; Notify(); }
    }

    public async Task RefreshAsync(IEnumerable<string> paths, CancellationToken cancellationToken, bool force = false)
    {
        var revision = selectionRevision;
        var pathToCheck = selectedPath;
        var isRepository = false;
        try { if (pathToCheck is not null) isRepository = await git.IsRepositoryAsync(pathToCheck, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { /* Unavailable Git or folder: hide the repository-only action. */ }
        if (revision == selectionRevision)
        {
            selectedIsRepository = isRepository;
            Notify();
        }
        if (refreshing || token is null) return;
        refreshing = true;
        var session = token;
        try
        {
            if (session.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                var refreshed = await authentication.RefreshAsync(session, cancellationToken);
                if (token != session) return;
                authentication.Save(refreshed);
                session = token = refreshed;
            }
            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var branch = await git.ReadAsync(path, cancellationToken);
                    if (token != session) return;
                    if (!branches.TryGetValue(path, out var previous) || branch != previous)
                    {
                        branches[path] = branch;
                        SetPull(path, null);
                        checkedAt.Remove(path);
                    }
                    if (branch is null) { if (path == selectedPath) Error = ""; continue; }
                    if (!force && checkedAt.TryGetValue(path, out var last) && DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(1))
                    {
                        PullRequestChanged?.Invoke(path, pulls.GetValueOrDefault(path));
                        continue;
                    }
                    checkedAt[path] = DateTimeOffset.UtcNow;
                    var pull = await api.FindPullRequestAsync(branch, session.AccessToken, cancellationToken);
                    if (token != session) return;
                    if (branch != await git.ReadAsync(path, cancellationToken)) { SetPull(path, null); checkedAt.Remove(path); continue; }
                    SetPull(path, pull);
                    if (path == selectedPath) Error = "";
                }
                catch (UnauthorizedAccessException) { Disconnect(); Error = "Your GitHub session expired. Connect again."; return; }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    SetPull(path, null);
                    if (path == selectedPath) Error = exception is IOException ? exception.Message : "Couldn't check this project's GitHub pull request. Check Git installation and network access.";
                }
            }
        }
        catch (UnauthorizedAccessException) { Disconnect(); Error = "Your GitHub session expired. Connect again."; }
        finally { refreshing = false; }
    }

    private void SetPull(string path, GitHubPullRequest? pull)
    {
        pulls[path] = pull;
        PullRequestChanged?.Invoke(path, pull);
        Notify();
    }
    private void Clear()
    {
        foreach (var path in pulls.Keys.ToArray()) SetPull(path, null);
        branches.Clear();
        checkedAt.Clear();
    }
    private void Notify()
    {
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(HeaderLabel));
        OnPropertyChanged(nameof(HeaderTooltip));
        OnPropertyChanged(nameof(ShowHeaderButton));
    }
}
