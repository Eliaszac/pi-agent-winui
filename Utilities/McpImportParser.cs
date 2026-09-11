using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Utilities;

/// <summary>Validates the supported MCP JSON shape while preserving adapter-specific server options.</summary>
public static class McpImportParser
{
    public static IReadOnlyList<McpImportEntry> Parse(string json, IReadOnlySet<string> existing)
    {
        if (json.Length > 262144) throw new ArgumentException("Import JSON is limited to 256 KB.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        CheckDuplicates(document.RootElement);
        if (JsonNode.Parse(json) is not JsonObject root || root["mcpServers"] is not JsonObject servers || servers.Count is 0 or > 128)
            throw new ArgumentException("Paste a JSON object containing mcpServers with 1–128 named servers.");
        var result = new List<McpImportEntry>();
        foreach (var (name, value) in servers)
        {
            if (name.Length is 0 or > 128 || name.Any(char.IsControl) || name is "__proto__" or "constructor" or "prototype" || value is not JsonObject server)
                throw new ArgumentException("Each server needs a valid name and a JSON object definition.");
            var command = Text(server, "command"); var url = Text(server, "url");
            if ((command.Length > 0) == (url.Length > 0) || server.ContainsKey("socket"))
                throw new ArgumentException($"{name}: provide either command for stdio or url for HTTP, not both.");
            var transport = command.Length > 0 ? "stdio" : "HTTP";
            var type = Text(server, "type");
            if (type.Length > 0 && !(transport == "stdio" ? type == "stdio" : type is "http" or "sse" or "streamable-http"))
                throw new ArgumentException($"{name}: transport type does not match command/url.");
            if (command.Contains('\n') || command.Contains('\r')) throw new ArgumentException($"{name}: put arguments in the args array.");
            if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme is not ("https" or "http") || address.UserInfo.Length > 0))
                throw new ArgumentException($"{name}: use an HTTP or HTTPS URL without embedded credentials.");
            if (server["args"] is { } args && (args is not JsonArray array || array.Any(item => item is not JsonValue v || !v.TryGetValue<string>(out _))))
                throw new ArgumentException($"{name}: args must be an array of strings.");
            foreach (var key in new[] { "env", "headers" })
                if (server[key] is { } map && (map is not JsonObject fields || fields.Any(field => field.Value is not JsonValue v || !v.TryGetValue<string>(out _))))
                    throw new ArgumentException($"{name}: {key} must contain string values.");
            result.Add(new(name, transport, server.ToJsonString(), existing.Contains(name)));
        }
        return result;
    }

    private static string Text(JsonObject root, string key)
    {
        if (root[key] is null) return "";
        if (root[key] is JsonValue value && value.TryGetValue<string>(out var text)) return text.Trim();
        throw new ArgumentException($"{key} must be a string.");
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ArgumentException("The JSON contains duplicate property names.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
    }
}
