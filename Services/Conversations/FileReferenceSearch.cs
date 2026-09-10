using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Bounded, cancellable filename lookup; file contents are never read by the picker.</summary>
public sealed class FileReferenceSearch
{
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".vs", ".idea", "node_modules", "bin", "obj", "artifacts", ".venv" };

    public Task<IReadOnlyList<FileReference>> FindAsync(string? root, string query, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<FileReference>>(() =>
    {
        var matches = new List<FileReference>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return matches;
        var directories = new Stack<string>();
        directories.Push(root);
        var visited = 0;
        while (directories.TryPop(out var directory) && visited < 8000 && matches.Count < 30)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++visited > 8000) break;
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!Excluded.Contains(Path.GetFileName(path))) directories.Push(path);
                    }
                    else
                    {
                        var relative = Path.GetRelativePath(root, path);
                        if (relative.Contains(query, StringComparison.OrdinalIgnoreCase)) matches.Add(new(path, relative));
                        if (matches.Count == 30) break;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return matches.OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }, cancellationToken);
}
