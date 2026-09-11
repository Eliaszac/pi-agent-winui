using System.Text.Json.Nodes;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Adds reviewed definitions to the adapter-owned global config without starting servers.</summary>
public sealed class McpConfigImporter(string agentDirectory, Func<bool>? adapterAvailable = null)
{
    private readonly GlobalConfigurationFile config = new(Path.Combine(agentDirectory, "mcp.json"));
    private void EnsureAdapter()
    {
        if (!(adapterAvailable?.Invoke() ?? !McpAdapterSupport.GetInstallationState(agentDirectory).NeedsSetup))
            throw new InvalidOperationException("Install the supported MCP Adapter from Extensions before importing servers.");
    }
    public async Task<IReadOnlyList<McpImportEntry>> PreviewAsync(string json, IEnumerable<string> runtimeNames)
    {
        EnsureAdapter();
        var current = await config.ReadAsync();
        if (current["mcpServers"] is not null && current["mcpServers"] is not JsonObject) throw new IOException("Existing mcpServers must be a JSON object.");
        var names = new HashSet<string>(runtimeNames, StringComparer.Ordinal);
        if (current["mcpServers"] is JsonObject servers) names.UnionWith(servers.Select(item => item.Key));
        return McpImportParser.Parse(json, names);
    }
    public async Task ImportAsync(IReadOnlyList<McpImportEntry> entries)
    {
        EnsureAdapter();
        var selected = entries.Where(entry => entry.Selected && entry.CanImport).ToArray();
        if (selected.Length == 0) throw new ArgumentException("Select at least one new server to import.");
        await config.UpdateAsync(root =>
        {
            if (root["mcpServers"] is not null && root["mcpServers"] is not JsonObject) throw new IOException("Existing mcpServers must be a JSON object.");
            var servers = root["mcpServers"] as JsonObject ?? new JsonObject();
            foreach (var entry in selected)
            {
                if (servers.ContainsKey(entry.Name)) throw new IOException($"{entry.Name} was added since the preview. Preview again before importing.");
                servers.Add(entry.Name, JsonNode.Parse(entry.Definition));
            }
            if (root["mcpServers"] is null) root["mcpServers"] = servers;
        });
    }
}
