using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Files;

/// <summary>Conversation-scoped, bounded file discovery. An incomplete scan never claims a unique match.</summary>
public sealed class WorkspaceFileLinks(ExecutionTarget target, Func<ExecutionTarget, string, int?, int?, Task> open) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private string[]? files;
    private DateTimeOffset expires;
    private string? lookupError;
    public ExecutionTarget Target => target;
    public Conversations.ArtifactStore? Artifacts { get; set; }
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) { ".git", "node_modules", "bin", "obj", ".vs", ".idea", ".venv", "__pycache__" };

    public async Task<IReadOnlyList<string>> ResolveAsync(FileMention mention, CancellationToken token, bool refresh = false)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        if (Artifacts is { } artifacts)
        {
            var matches = new List<string>();
            foreach (var record in await artifacts.ListAsync(linked.Token).ConfigureAwait(false))
            {
                if (record.Deleted) continue;
                var candidate = mention.Path.Replace('\\', '/');
                string path;
                try { path = await artifacts.GetPathAsync(record.Id).ConfigureAwait(false); }
                catch (IOException) { continue; }
                if (candidate.Equals(record.Name, StringComparison.OrdinalIgnoreCase)
                    || candidate.Equals(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) matches.Add(path);
            }
            if (matches.Count > 0) return matches;
        }
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (refresh) expires = DateTimeOffset.MinValue;
            if (lookupError is not null && DateTimeOffset.UtcNow < expires) throw new IOException(lookupError);
            if (files is null || DateTimeOffset.UtcNow >= expires)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    files = target.IsLocal ? await Task.Run(() => ScanLocal(timeout.Token), timeout.Token).ConfigureAwait(false)
                        : await ScanRemoteAsync(timeout.Token).ConfigureAwait(false);
                    lookupError = null;
                    expires = DateTimeOffset.UtcNow.AddSeconds(15);
                }
                catch (Exception exception) when (!linked.IsCancellationRequested && exception is IOException or UnauthorizedAccessException or OperationCanceledException)
                {
                    files = null;
                    lookupError = "File lookup is unavailable or exceeded its limits. Check the workspace connection and permissions.";
                    expires = DateTimeOffset.UtcNow.AddSeconds(15);
                    throw new IOException(lookupError, exception);
                }
            }
            var snapshot = files;
            return await Task.Run(() => Match(target, snapshot, mention.Path), linked.Token).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public static IReadOnlyList<string> Match(ExecutionTarget target, IEnumerable<string> relativeFiles, string candidate)
    {
        var comparison = target.IsLocal ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var path = candidate.Replace('\\', '/');
        var explicitPath = path.Contains('/');
        var root = target.Path.Replace('\\', '/').TrimEnd('/');
        if (path.StartsWith(root + "/", comparison)) path = path[(root.Length + 1)..];
        else if (path.StartsWith('/') || path.Contains(':')) return [];
        while (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
        if (path.Split('/').Any(part => part is ".." or "")) return [];
        return relativeFiles.Where(file => string.Equals(explicitPath ? file : file[(file.LastIndexOf('/') + 1)..], path, comparison))
            .Order(StringComparer.Ordinal).ToArray();
    }

    public string FullPath(string relative) => Path.IsPathFullyQualified(relative) ? relative : target.IsLocal ? Path.Combine(target.Path, relative.Replace('/', Path.DirectorySeparatorChar)) : target.Path.TrimEnd('/') + "/" + relative;

    public Task OpenExactAsync(string path, CancellationToken token)
    {
        var normalized = path.Replace('\\', '/');
        var root = target.Path.Replace('\\', '/').TrimEnd('/');
        if (normalized.StartsWith(root + "/", target.IsLocal ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            normalized = normalized[(root.Length + 1)..];
        if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.Split('/').Any(part => part is ".." or ""))
            throw new IOException("This file is outside the conversation workspace.");
        return OpenAsync(normalized, new(0, path.Length, path, path, null, null), token);
    }

    public async Task OpenAsync(string relative, FileMention mention, CancellationToken token)
    {
        if (Artifacts is { } artifacts && Path.IsPathFullyQualified(relative))
        {
            foreach (var record in await artifacts.ListAsync(token).ConfigureAwait(false))
            {
                if (record.Deleted) continue;
                string path;
                try { path = await artifacts.GetPathAsync(record.Id).ConfigureAwait(false); }
                catch (IOException) { continue; }
                if (!path.Equals(relative, StringComparison.OrdinalIgnoreCase)) continue;
                await open(new ExecutionTarget { Id = target.Id, Name = "Conversation artifacts", Path = artifacts.DirectoryPath }, path, mention.Line, mention.Column).ConfigureAwait(false);
                return;
            }
            throw new IOException("This artifact is no longer available.");
        }
        var full = FullPath(relative);
        if (target.IsLocal)
        {
            await Task.Run(() =>
            {
                new ProjectFileSystem(target.Path, (_, _) => throw new NotSupportedException()).Validate(full);
                if (!File.Exists(full)) throw new IOException("The file no longer exists. Try the link again to refresh its matches.");
            }, token).ConfigureAwait(false);
        }
        else await new TargetFileReader(target, new TargetCommandRunner()).EnsureFileAsync(full, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await open(target, full, mention.Line, mention.Column).ConfigureAwait(false);
    }

    private string[] ScanLocal(CancellationToken token)
    {
        var guard = new ProjectFileSystem(target.Path, (_, _) => throw new NotSupportedException());
        guard.Validate(target.Path, allowRoot: true);
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((target.Path, 0));
        var result = new List<string>();
        var entries = 0;
        while (pending.TryPop(out var folder))
        {
            token.ThrowIfCancellationRequested();
            foreach (var item in new DirectoryInfo(folder.Path).EnumerateFileSystemInfos())
            {
                token.ThrowIfCancellationRequested();
                if (++entries > 50000) throw new IOException("Workspace file lookup exceeded 50,000 entries.");
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((item.Attributes & FileAttributes.Directory) != 0)
                {
                    if (Excluded.Contains(item.Name)) continue;
                    if (folder.Depth >= 32) throw new IOException("Workspace file lookup exceeded its folder-depth limit.");
                    pending.Push((item.FullName, folder.Depth + 1));
                }
                else result.Add(Path.GetRelativePath(target.Path, item.FullName).Replace('\\', '/'));
            }
        }
        return result.ToArray();
    }

    private async Task<string[]> ScanRemoteAsync(CancellationToken token)
    {
        var exclusions = string.Join(" -o ", Excluded.Select(name => "-name " + PosixShell.Quote(name)));
        var command = "test ! -L " + PosixShell.Quote(target.Path) + " && cd -- " + PosixShell.Quote(target.Path) + " && find -P . -type d \\( " + exclusions + " \\) -prune -o -type f -print0";
        var result = await new TargetCommandRunner().RunAsync(target, command, token, trackChanges: false).ConfigureAwait(false);
        if (result.ExitCode != 0) throw new IOException("Couldn't look up files on this target. Check its connection and permissions.");
        var paths = result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length > 50000) throw new IOException("Workspace file lookup exceeded 50,000 files.");
        return paths.Select(path => path.StartsWith("./", StringComparison.Ordinal) ? path[2..] : path).ToArray();
    }

    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}
