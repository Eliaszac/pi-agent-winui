using PiAgentGui.Utilities;

namespace PiAgentGui.Services.SourceControl;

public static class UntrackedFileDiff
{
    public static async Task<string> ReadAsync(string root, string relativePath, CancellationToken token)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("This file is outside the repository.");
        for (var current = path; current.Length > normalizedRoot.Length; current = Path.GetDirectoryName(current)!)
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked files are not loaded by the diff viewer.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, true);
        if (stream.Length > 2_000_000) throw new IOException("This file is too large for the text diff viewer (2 MB limit).");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);
        var buffer = new char[8192]; var text = new System.Text.StringBuilder(); int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (buffer.AsSpan(0, count).Contains('\0')) throw new IOException("Binary files don't have a text diff.");
            if (text.Length + count > 2_000_000) throw new IOException("This file is too large for the text diff viewer (2 MB limit).");
            text.Append(buffer, 0, count);
        }
        return NewFilePatch.Create(text.ToString());
    }
}
