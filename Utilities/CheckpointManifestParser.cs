using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

public static class CheckpointManifestParser
{
    private static string? ValidHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;

    public static CheckpointManifest? Parse(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return null;
        var id = PiJson.Text(value, "id");
        var response = PiJson.Text(value, "response");
        var files = PiJson.Field(value, "files");
        if (!Guid.TryParseExact(id, "N", out _) || !response.StartsWith("assistant:", StringComparison.Ordinal) ||
            files.ValueKind != JsonValueKind.Array || files.GetArrayLength() > 20_000) return null;
        var changes = new List<FileChange>();
        foreach (var item in files.EnumerateArray())
        {
            var path = PiJson.Text(item, "path");
            var kind = PiJson.Text(item, "kind");
            var patch = PiJson.Field(item, "patch");
            if (path.Length is 0 or > 4096 || kind is not ("created" or "modified" or "deleted")) return null;
            var beforeHash = ValidHash(PiJson.Text(item, "beforeHash"));
            var afterHash = ValidHash(PiJson.Text(item, "afterHash"));
            changes.Add(new(path, patch.ValueKind == JsonValueKind.String ? patch.GetString() : null,
                Math.Max(0, PiJson.Number(item, "added")), Math.Max(0, PiJson.Number(item, "removed")), null, kind, beforeHash, afterHash));
        }
        var applied = PiJson.Field(value, "applied");
        return new(id, response, PiJson.Text(value, "state"), PiJson.Flag(value, "overlap"), Math.Max(0, PiJson.Number(value, "omitted")), changes,
            applied.ValueKind == JsonValueKind.Array ? applied.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : []);
    }
}
