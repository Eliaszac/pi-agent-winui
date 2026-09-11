using System.Security.Cryptography;
using System.Text;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Bounded UTF-8 instruction reads and conflict-checked atomic file replacement.</summary>
public sealed class InstructionFileStore
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> SaveGates = new(StringComparer.OrdinalIgnoreCase);
    public async Task<InstructionDocument> ReadAsync(string path)
    {
        ValidatePath(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
        if (file.Length > 262144) throw new IOException("Instruction files larger than 256 KB cannot be edited here.");
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int length;
        while ((length = await file.ReadAsync(buffer)) > 0)
        {
            if (bytes.Length + length > 262144) throw new IOException("Instruction file exceeds the 256 KB limit.");
            bytes.Write(buffer, 0, length);
        }
        var content = bytes.ToArray();
        string text;
        try { text = Utf8.GetString(content); }
        catch (DecoderFallbackException) { throw new IOException("This editor supports UTF-8 instruction files only."); }
        if (text.Contains('\0')) throw new IOException("This editor supports UTF-8 text instruction files only.");
        var hasBom = text.StartsWith('\uFEFF');
        var hash = Convert.ToHexString(SHA256.HashData(content));
        return new(path, hasBom ? text[1..] : text, hash, hash, hasBom,
            text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : text.Contains('\r') ? "\r" : "\n");
    }

    public async Task SaveAsync(InstructionDocument original, string text)
    {
        var gate = SaveGates.GetOrAdd(Path.GetFullPath(original.Path), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try { await SaveCoreAsync(original, text); }
        finally { gate.Release(); }
    }

    private async Task SaveCoreAsync(InstructionDocument original, string text)
    {
        ValidatePath(original.Path);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", original.NewLine, StringComparison.Ordinal);
        var bytes = Utf8.GetBytes((original.HasBom ? "\uFEFF" : "") + normalized);
        if (bytes.Length > 262144) throw new IOException("Instruction file exceeds the 256 KB limit.");
        var temporary = original.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes);
            var current = await ReadAsync(original.Path);
            if (current.DiskHash != original.DiskHash) throw new IOException("This file changed outside the editor. Cancel and reopen it before saving.");
            File.Replace(temporary, original.Path, null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidatePath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || Path.GetFileName(path).ToUpperInvariant() is not ("AGENTS.MD" or "AGENTS.OVERRIDE.MD" or "CLAUDE.MD"))
            throw new IOException("Only loaded AGENTS.md, AGENTS.override.md and CLAUDE.md files can be edited here.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Edit this linked instruction file in your external editor.");
    }
}
