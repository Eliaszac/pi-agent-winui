using PiAgentGui.Models.SourceControl;

namespace PiAgentGui.Services.SourceControl;

public static class UntrackedFileStats
{
    public static async Task<GitChange> ReadAsync(string root, GitChange change, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(root, change.Path));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return change;
        try
        {
            if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) return change;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, true);
            if (stream.Length > 8_000_000) return change;
            var buffer = new byte[8192]; long lines = 0; long bytes = 0; byte last = 0;
            int count;
            while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                for (var index = 0; index < count; index++)
                {
                    if (bytes + index < 8000 && buffer[index] == 0) return change with { Binary = true };
                    if (buffer[index] == 10) lines++;
                }
                bytes += count; last = buffer[count - 1];
                if (bytes > 8_000_000) return change;
            }
            if (bytes > 0 && last != 10) lines++;
            return change with { Added = lines, Removed = 0 };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return change; }
    }
}
