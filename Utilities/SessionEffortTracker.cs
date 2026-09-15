using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>Tracks reasoning metadata along session branches without retaining message content.</summary>
public sealed class SessionEffortTracker
{
    private readonly Dictionary<string, string?> levels = [];
    private string? previous;

    public string? Read(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object) return null;
        var level = entry.TryGetProperty("parentId", out var parent)
            ? parent.ValueKind == JsonValueKind.String && levels.TryGetValue(parent.GetString()!, out var inherited) ? inherited : null
            : previous;
        if (PiJson.Text(entry, "type") == "thinking_level_change")
            level = PiJson.Text(entry, "thinkingLevel") is { Length: > 0 } value ? value : null;
        var id = PiJson.Text(entry, "id");
        if (id.Length > 0 && levels.Count < 200_000) levels[id] = level;
        previous = level;
        return level;
    }
}
