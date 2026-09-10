using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Reads authoritative tool-result patches without inferring deleted content from today's files.</summary>
public static class FileChangeParser
{
    public static FileChange? Parse(string tool, JsonElement input, JsonElement details)
    {
        if (tool is not ("write" or "edit")) return null;
        var capture = PiJson.Field(details, "piGuiFileChange");
        var path = PiJson.Text(capture, "path");
        if (path.Length == 0) path = PiJson.Text(input, "path");
        var patchField = capture.ValueKind == JsonValueKind.Object ? PiJson.Field(capture, "patch") : PiJson.Field(details, "patch");
        var patch = patchField.ValueKind == JsonValueKind.String ? patchField.GetString() : null;
        var added = 0;
        var removed = 0;
        var inHunk = false;
        foreach (var line in (patch ?? "").Split('\n'))
        {
            if (line.StartsWith("@@", StringComparison.Ordinal)) { inHunk = true; continue; }
            if (!inHunk) continue;
            if (line.StartsWith('+')) added++;
            if (line.StartsWith('-')) removed++;
        }
        return new(path, patch, added, removed, patch is not null ? null :
            PiJson.Text(capture, "unavailable") is { Length: > 0 } reason ? reason : "Diff unavailable for this saved tool result.");
    }
}
