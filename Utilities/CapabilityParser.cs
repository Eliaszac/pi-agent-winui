using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Validates and bounds read-only runtime inventory data.</summary>
public static class CapabilityParser
{
    public const string McpStatusKey = "pi-gui-mcp-status-v1";

    public static IReadOnlyList<AvailableSkill> Skills(JsonElement commands)
    {
        if (commands.ValueKind != JsonValueKind.Array) return [];
        return commands.EnumerateArray().Where(item => PiJson.Text(item, "source") == "skill")
            .Select(item =>
            {
                var name = PiJson.Text(item, "name");
                var source = PiJson.Field(item, "sourceInfo");
                var scope = PiJson.Text(source, "scope");
                if (scope.Length == 0) scope = PiJson.Text(item, "location");
                var path = PiJson.Text(source, "path");
                if (path.Length == 0) path = PiJson.Text(item, "path");
                return new AvailableSkill(name.StartsWith("skill:", StringComparison.Ordinal) ? name[6..] : "",
                    PiJson.Text(item, "description"), scope switch { "user" => "Global", "project" => "Project", _ => "Custom" }, path);
            }).Where(item => item.Name.Length is > 0 and <= 256 && !item.Name.Any(char.IsWhiteSpace) && !item.Name.Contains('/'))
            .DistinctBy(item => item.Name).Take(2000).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static McpStatusSnapshot? Mcp(string json)
    {
        if (json.Length > 262144) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (PiJson.Number(root, "version") != 1) return null;
            var servers = PiJson.Field(root, "servers");
            if (servers.ValueKind != JsonValueKind.Array || servers.GetArrayLength() > 256) return null;
            var rows = new List<McpServerStatus>();
            foreach (var item in servers.EnumerateArray())
            {
                var name = PiJson.Text(item, "name");
                var state = PiJson.Text(item, "status");
                if (name.Length is 0 or > 256 || state is not ("connected" or "cached" or "failed" or "needs-auth" or "not-connected" or "disabled")) return null;
                if (!PiJson.Field(item, "toolCount").TryGetInt32(out var tools) || tools < 0) return null;
                var resource = PiJson.Field(item, "resourceCount");
                int? resources = resource.ValueKind == JsonValueKind.Number && resource.TryGetInt32(out var count) && count >= 0 ? count : null;
                rows.Add(new(name, state, tools, resources));
            }
            return new(rows.DistinctBy(row => row.Name).OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return null; }
    }
}
