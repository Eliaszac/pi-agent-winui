using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Reads a bounded Markdown preview only after an explicit skill selection.</summary>
public static class SkillPreviewReader
{
    public static async Task<string> ReadAsync(AvailableSkill skill)
    {
        if (!System.IO.Path.IsPathFullyQualified(skill.Path) || !skill.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Pi did not provide a local Markdown path for this skill.");
        using var stream = new FileStream(skill.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        if (stream.Length > 262144) throw new IOException("This skill is too large to preview (limit: 256 KB).");
        using var reader = new StreamReader(stream);
        var buffer = new char[131073];
        var length = await reader.ReadBlockAsync(buffer.AsMemory());
        if (length > 131072) throw new IOException("This skill is too large to preview.");
        return new string(buffer, 0, length);
    }
}
