using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Reads configured names without loading servers or exposing credentials.</summary>
public sealed class McpConfiguredInventory(string agentDirectory)
{
    public async Task<IReadOnlyList<McpServerStatus>> ReadAsync()
    {
        var root = await new GlobalConfigurationFile(Path.Combine(agentDirectory, "mcp.json")).ReadAsync();
        if (root["mcpServers"] is null) return [];
        if (root["mcpServers"] is not JsonObject servers) throw new IOException("mcpServers must be an object.");
        return servers.Select(entry => new McpServerStatus(entry.Key,
            entry.Value is JsonObject obj && obj["disabled"] is JsonValue v && v.TryGetValue<bool>(out var disabled) && disabled
                ? "disabled" : "configured", 0, null)).ToArray();
    }
}
