using PiAgentGui.Models.SourceControl;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.SourceControl;

public sealed class GitRepositoryService(IGitCommandRunner runner, Action<string, bool>? recycle = null)
{
    public Task<MergeConflict> ReadConflictAsync(GitSnapshot state, GitChange change, CancellationToken token) => new GitConflictService(runner).ReadAsync(state, change, token);
    public Task ApplyConflictAsync(MergeConflict conflict, string result, bool delete, CancellationToken token) => new GitConflictService(runner).ApplyAsync(conflict, result, delete, token);
    public async Task RevertAsync(GitSnapshot state, GitChange change, CancellationToken token)
    {
        if (!change.CanRevert || !state.Changes.Contains(change)) throw new InvalidOperationException("Refresh the file list before reverting this file.");
        await VerifyHeadAsync(state, token);
        var status = await RequireAsync(state.Root, ["status", "--porcelain=v1", "-z", "--untracked-files=all", "--no-renames", "--", change.Path], token);
        if (!GitStatusParser.Parse(status, "", "").Any(item => item.Path == change.Path && !item.Staged && item.Status == change.Status))
            throw new InvalidOperationException("The file status changed. Refresh and try again.");
        if (change.Status == '?')
        {
            var files = new Files.ProjectFileSystem(state.Root, recycle ?? Files.RecycleBin.Delete);
            var path = Path.GetFullPath(Path.Combine(state.Root, change.Path));
            files.Validate(path);
            if (!File.Exists(path) || Directory.Exists(path)) throw new IOException("Only individual untracked files can be reverted.");
            token.ThrowIfCancellationRequested();
            files.Delete(path);
        }
        else await RequireAsync(state.Root, ["restore", "--worktree", "--", change.Path], token);
    }

    public async Task<Models.Conversations.FileDiffContent> ReadDiffAsync(GitSnapshot state, GitChange change, CancellationToken token)
    {
        if (!state.Changes.Contains(change)) throw new InvalidOperationException("Refresh the file list before opening this diff.");
        await VerifyHeadAsync(state, token);
        var description = change.Staged ? "Staged changes" : change.Status == '?' ? "Untracked file" : "Working-tree changes";
        var left = change.Staged ? "HEAD" : change.Status == '?' ? "Empty file" : "Index";
        var right = change.Staged ? "Index" : "Working tree";
        if (change.Status == 'U') return new(change.Path, "", description, left, right, "Open this file from the Merge conflicts group to resolve it in the three-way merge editor.");
        if (change.Binary) return new(change.Path, "", description, left, right, "Binary files don't have a text diff.");
        string patch;
        if (change.Status == '?') patch = await UntrackedFileDiff.ReadAsync(state.Root, change.Path, token);
        else
        {
            var arguments = new List<string> { "diff", "--patch", "--unified=2147483647", "--no-renames", "--no-ext-diff", "--no-textconv", "--src-prefix=a/", "--dst-prefix=b/", "--output-indicator-new=+", "--output-indicator-old=-", "--output-indicator-context= " };
            if (change.Staged) arguments.Add("--cached");
            arguments.Add("--"); arguments.Add(change.Path);
            patch = await RequireAsync(state.Root, arguments.ToArray(), token);
        }
        await VerifyHeadAsync(state, token);
        return new(change.Path, patch, description, left, right,
            patch.Length == 0 ? "No text changes to display. The file may have changed since the last refresh, or only its metadata changed." : "");
    }

    public async Task<GitSnapshot?> ReadAsync(string directory, CancellationToken token)
    {
        var inside = await runner.RunAsync(directory, ["rev-parse", "--is-inside-work-tree"], token);
        if (inside.ExitCode != 0)
        {
            if (inside.Error.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)) return null;
            throw new IOException(GitErrorMessage.Format(inside.Error));
        }
        if (inside.Output.Trim() != "true") return null;
        var root = (await RequireAsync(directory, ["rev-parse", "--show-toplevel"], token)).TrimEnd('\r', '\n');
        var head = await HeadAsync(root, token);
        var exists = await runner.RunAsync(root, ["rev-parse", "--verify", "HEAD"], token);
        var status = await RequireAsync(root, ["status", "--porcelain=v1", "-z", "--untracked-files=all", "--no-renames"], token);
        var staged = await RequireAsync(root, ["diff", "--cached", "--numstat", "-z", "--no-renames", "--no-ext-diff", "--no-textconv"], token);
        var working = await RequireAsync(root, ["diff", "--numstat", "-z", "--no-renames", "--no-ext-diff", "--no-textconv"], token);
        var changes = GitStatusParser.Parse(status, staged, working).ToArray();
        for (var index = 0; index < changes.Length; index++)
            if (changes[index].Status == '?') changes[index] = await UntrackedFileStats.ReadAsync(root, changes[index], token);
        var remotes = (await RequireAsync(root, ["remote"], token)).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var refs = await RequireAsync(root, ["for-each-ref", "--format=%(refname)%00%(symref)%00%(upstream:remotename)%00%(upstream:remoteref)", "refs/heads/", "refs/remotes/"], token);
        if (head != await HeadAsync(root, token)) throw new IOException("The branch changed while refreshing. Refresh again.");
        return new(root, head, exists.ExitCode == 0, changes, GitBranchParser.Parse(refs, head, remotes), remotes, changes.Any(change => change.Status == 'U'));
    }

    public async Task StageAsync(GitSnapshot state, GitChange? change, CancellationToken token)
    {
        if ((change is null && state.Conflicted) || change?.Status == 'U') throw new InvalidOperationException("Use the conflict resolver before staging conflicted files.");
        await VerifyHeadAsync(state, token);
        if ((await RequireAsync(state.Root, ["ls-files", "--unmerged", "-z", "--", change?.Path ?? "."], token)).Length > 0)
            throw new InvalidOperationException("These files now contain merge conflicts. Refresh and use the conflict resolver.");
        await RequireAsync(state.Root, change is null ? ["add", "--all", "--", "."] : ["add", "--all", "--", change.Path], token);
    }

    public async Task UnstageAsync(GitSnapshot state, GitChange? change, CancellationToken token)
    {
        await VerifyHeadAsync(state, token);
        var path = change?.Path ?? ".";
        await RequireAsync(state.Root, state.HasHead ? ["restore", "--staged", "--", path] : ["rm", "--cached", "-r", "--ignore-unmatch", "--", path], token);
    }

    public async Task CommitAsync(GitSnapshot state, string message, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new InvalidOperationException("Enter a commit message.");
        if (state.Detached) throw new InvalidOperationException("Check out a branch before committing.");
        await VerifyHeadAsync(state, token);
        await RequireAsync(state.Root, ["commit", "-m", message], token);
    }

    public async Task PushAsync(GitSnapshot state, string? remote, CancellationToken token)
    {
        if (state.Detached || !state.HasHead) throw new InvalidOperationException("Commit on a local branch before pushing.");
        await VerifyHeadAsync(state, token);
        var branch = state.Branches.FirstOrDefault(item => item.Current);
        var destination = branch?.CanFetch == true ? branch.RemoteRef! : state.HeadRef;
        var name = branch?.CanFetch == true ? branch.RemoteName : remote;
        ValidateRemote(state, name);
        await RequireAsync(state.Root, ["-c", "push.followTags=false", "push", "--set-upstream", "--", name!, "HEAD:" + destination], token);
    }

    public async Task FetchAsync(GitSnapshot state, GitBranch? branch, CancellationToken token)
    {
        if (branch is null) { await RequireAsync(state.Root, ["fetch", "--all"], token); return; }
        ValidateBranch(state, branch);
        if (!branch.CanFetch) throw new InvalidOperationException("This branch has no remote upstream to fetch.");
        ValidateRemote(state, branch.RemoteName);
        var destination = "refs/remotes/" + branch.RemoteName + "/" + branch.RemoteRef![11..];
        await RequireAsync(state.Root, ["fetch", "--", branch.RemoteName!, branch.RemoteRef + ":" + destination], token);
    }

    public async Task PullAsync(GitSnapshot state, CancellationToken token)
    {
        await VerifyHeadAsync(state, token);
        var branch = state.Branches.FirstOrDefault(item => item.Current);
        if (branch?.CanFetch != true) throw new InvalidOperationException("This branch has no remote upstream. Push it first or configure its upstream in Git.");
        ValidateRemote(state, branch.RemoteName);
        await RequireAsync(state.Root, ["pull", "--ff-only", "--no-rebase", "--no-autostash", "--", branch.RemoteName!, branch.RemoteRef!], token);
    }

    public async Task CheckoutAsync(GitSnapshot state, GitBranch branch, string? newName, CancellationToken token)
    {
        ValidateBranch(state, branch); await VerifyHeadAsync(state, token);
        if (newName is not null)
        {
            if (string.IsNullOrWhiteSpace(newName) || newName.StartsWith('-')) throw new InvalidOperationException("Enter a valid new branch name.");
            await RequireAsync(state.Root, ["check-ref-format", "refs/heads/" + newName], token);
            await RequireAsync(state.Root, ["switch", "--no-guess", "--no-track", "-c", newName, branch.Ref], token);
        }
        else if (branch.Remote)
        {
            if (!branch.CanFetch) throw new InvalidOperationException("This remote branch cannot be checked out.");
            await RequireAsync(state.Root, ["switch", "--no-guess", "--track", "-c", branch.RemoteRef![11..], branch.Ref], token);
        }
        else await RequireAsync(state.Root, ["switch", "--no-guess", "--", branch.Name], token);
    }

    public async Task MergeAsync(GitSnapshot state, GitBranch branch, CancellationToken token)
    {
        ValidateBranch(state, branch); await VerifyHeadAsync(state, token);
        if (state.Detached || !state.HasHead) throw new InvalidOperationException("Check out a committed local branch before merging.");
        await RequireAsync(state.Root, ["merge", "--no-edit", "--no-autostash", branch.Ref], token);
    }

    private static void ValidateRemote(GitSnapshot state, string? remote)
    {
        if (remote is null || !state.Remotes.Contains(remote) || remote.StartsWith('-')) throw new InvalidOperationException("Select a configured remote first.");
    }
    private static void ValidateBranch(GitSnapshot state, GitBranch branch)
    {
        if (!state.Branches.Contains(branch)) throw new InvalidOperationException("Refresh the branch list first.");
    }
    private async Task VerifyHeadAsync(GitSnapshot state, CancellationToken token)
    {
        if (state.HeadRef != await HeadAsync(state.Root, token)) throw new IOException("The current branch changed. Refresh before trying again.");
    }
    private async Task<string> HeadAsync(string root, CancellationToken token)
    {
        var result = await runner.RunAsync(root, ["symbolic-ref", "--quiet", "HEAD"], token);
        return result.ExitCode == 0 ? result.Output.Trim() : (await RequireAsync(root, ["rev-parse", "--verify", "HEAD"], token)).Trim();
    }
    private async Task<string> RequireAsync(string root, string[] args, CancellationToken token)
    {
        var result = await runner.RunAsync(root, args, token);
        if (result.ExitCode != 0) throw new IOException(GitErrorMessage.Format(result.Error.Length > 0 ? result.Error : result.Output));
        return result.Output;
    }
}
