using System.Text;
using System.Text.RegularExpressions;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.Files;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.SourceControl;

/// <summary>Reads unmerged index stages and applies one explicitly reviewed text resolution.</summary>
public sealed class GitConflictService(IGitCommandRunner runner)
{
    private const int MaximumBytes = 256_000;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public async Task<MergeConflict> ReadAsync(GitSnapshot state, GitChange change, CancellationToken token)
    {
        if (change.Status != 'U' || !state.Changes.Contains(change)) throw new InvalidOperationException("Refresh the conflict list first.");
        var files = new ProjectFileSystem(state.Root, RecycleBin.Delete);
        var path = Path.GetFullPath(Path.Combine(state.Root, change.Path));
        files.Validate(path);
        var head = await RequireAsync(state.Root, ["rev-parse", "--verify", "HEAD"], token);
        var entries = await EntriesAsync(state.Root, change.Path, token);
        var stages = new Dictionary<int, string>();
        foreach (var row in entries.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = row.IndexOf('\t');
            if (tab < 0 || row[(tab + 1)..] != change.Path) throw new IOException("Git returned an unexpected conflict path.");
            var fields = row[..tab].Split(' ');
            if (fields.Length != 3 || fields[0] is not ("100644" or "100755")
                || !Regex.IsMatch(fields[1], "^[0-9a-fA-F]{40,64}$") || !int.TryParse(fields[2], out var stage) || stage is < 1 or > 3)
                throw new IOException("Resolve symbolic-link, submodule, or file-type conflicts in your editor or Git client.");
            var size = await RequireAsync(state.Root, ["cat-file", "-s", fields[1]], token);
            if (!long.TryParse(size.Trim(), out var bytes) || bytes > MaximumBytes) throw new IOException("This conflict is too large for the merge editor (250 KB limit). Resolve it in your editor.");
            var text = await RequireAsync(state.Root, ["cat-file", "blob", fields[1]], token);
            ValidateText(text);
            stages.Add(stage, text.TrimStart('\uFEFF'));
        }
        if (stages.Count == 0) throw new IOException("This file is no longer conflicted. Refresh the list.");
        var original = await ReadWorkingAsync(path, token);
        var bom = original is [0xEF, 0xBB, 0xBF, ..];
        string working;
        try { working = original is null ? "" : Utf8.GetString(original.AsSpan(bom ? 3 : 0)); }
        catch (DecoderFallbackException exception) { throw new IOException("This file is not UTF-8 text. Resolve it in your editor to preserve its encoding.", exception); }
        ValidateText(working);
        if (entries != await EntriesAsync(state.Root, change.Path, token) || head != await RequireAsync(state.Root, ["rev-parse", "--verify", "HEAD"], token)) throw new IOException("The conflict changed while loading. Reopen it.");
        return new(state.Root, change.Path, head, entries, stages.GetValueOrDefault(1), stages.GetValueOrDefault(2), stages.GetValueOrDefault(3), original, working, bom);
    }

    public async Task ApplyAsync(MergeConflict conflict, string result, bool delete, CancellationToken token)
    {
        ValidateText(result);
        if (!delete && ConflictMarkers.HasMarkers(result)) throw new InvalidOperationException("Resolve all conflict markers before applying.");
        if (delete && conflict.Left is not null && conflict.Right is not null) throw new InvalidOperationException("Neither side deleted this file.");
        var files = new ProjectFileSystem(conflict.Root, RecycleBin.Delete);
        var path = Path.GetFullPath(Path.Combine(conflict.Root, conflict.Path));
        files.Validate(path);
        if (conflict.Head != await RequireAsync(conflict.Root, ["rev-parse", "--verify", "HEAD"], token)
            || conflict.IndexEntries != await EntriesAsync(conflict.Root, conflict.Path, token))
            throw new IOException("Git changed while this resolver was open. Reopen the conflict before applying.");
        var current = await ReadWorkingAsync(path, token);
        if (!(current is null && conflict.OriginalBytes is null) && (current is null || conflict.OriginalBytes is null || !current.SequenceEqual(conflict.OriginalBytes)))
            throw new IOException("This file was edited outside the resolver. Reopen it to avoid overwriting those edits.");
        token.ThrowIfCancellationRequested();
        if (delete) { if (current is not null) files.Delete(path); }
        else
        {
            var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".pi-merge-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllTextAsync(temporary, result, new UTF8Encoding(conflict.HasBom, true), token);
                files.Validate(path);
                var latest = await ReadWorkingAsync(path, token);
                if (!(latest is null && current is null) && (latest is null || current is null || !latest.SequenceEqual(current)))
                    throw new IOException("This file changed while saving. Reopen the resolver to preserve those edits.");
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        try { await RequireAsync(conflict.Root, ["add", "--all", "--", conflict.Path], token); }
        catch (Exception exception) { throw new IOException("The result was saved, but staging failed. Refresh the panel and stage the file after checking it. " + exception.Message, exception); }
    }

    private Task<string> EntriesAsync(string root, string path, CancellationToken token) => RequireAsync(root, ["ls-files", "--unmerged", "-z", "--", path], token);
    private async Task<string> RequireAsync(string root, string[] args, CancellationToken token)
    {
        var result = await runner.RunAsync(root, args, token);
        if (result.ExitCode != 0) throw new IOException(GitErrorMessage.Format(result.Error));
        return result.Output;
    }
    private static async Task<byte[]?> ReadWorkingAsync(string path, CancellationToken token)
    {
        if (Directory.Exists(path)) throw new IOException("Resolve directory conflicts using your editor or Git client.");
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > MaximumBytes) throw new IOException("This conflict is too large for the merge editor (250 KB limit).");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, token);
        return bytes;
    }
    private static void ValidateText(string text)
    {
        if (text.Length > MaximumBytes || Utf8.GetByteCount(text) > MaximumBytes || text.Contains('\0') || text.Contains('\uFFFD'))
            throw new IOException("This merge editor supports UTF-8 text files up to 250 KB. Resolve binary or other encoded files in your editor.");
    }
}
