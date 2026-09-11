using System.Text.Json.Nodes;

namespace PiAgentGui.Utilities;

/// <summary>Converts manual fields to the same validated configuration used by JSON import.</summary>
public static class McpServerDefinitionBuilder
{
    public static string Build(string name, bool stdio, string command, string arguments, string environment, string url, string headers)
    {
        name = name.Trim();
        var server = new JsonObject();
        if (stdio)
        {
            server["command"] = command.Trim();
            var args = new JsonArray();
            foreach (var line in Lines(arguments)) args.Add(line);
            if (args.Count > 0) server["args"] = args;
            var env = Pairs(environment, "Environment variables");
            if (env.Count > 0) server["env"] = env;
        }
        else
        {
            server["url"] = url.Trim();
            var values = Pairs(headers, "Headers");
            if (values.Count > 0) server["headers"] = values;
        }
        var json = new JsonObject { ["mcpServers"] = new JsonObject { [name] = server } }.ToJsonString();
        McpImportParser.Parse(json, new HashSet<string>());
        return json;
    }

    private static IEnumerable<string> Lines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(line => !string.IsNullOrWhiteSpace(line));
    private static JsonObject Pairs(string text, string label)
    {
        var values = new JsonObject();
        foreach (var line in Lines(text))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) throw new ArgumentException($"{label}: use one NAME=value per line.");
            var key = line[..separator].Trim();
            if (key.Length == 0 || key.Any(char.IsControl)) throw new ArgumentException($"{label}: enter a valid name before =.");
            if (values.ContainsKey(key)) throw new ArgumentException($"{label}: each name must be unique.");
            values.Add(key, line[(separator + 1)..]);
        }
        return values;
    }
}
