using System.Text.Json.Nodes;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Maintains one reviewed global definition without overwriting concurrent edits.</summary>
public sealed class McpSetupService(string agentDirectory, Func<bool>? adapterAvailable = null)
{
    private readonly GlobalConfigurationFile config = new(Path.Combine(agentDirectory, "mcp.json"));
    public Task RemoveAsync(string name, JsonObject expected) => config.UpdateAsync(root =>
    {
        if (root["mcpServers"] is not JsonObject servers || !JsonNode.DeepEquals(servers[name], expected))
            throw new IOException("This server changed in another window. Close and reopen settings before removing it.");
        servers.Remove(name);
    });

    public Task EditAsync(string name, JsonObject expected, string json)
    {
        // Validate the original text before parsing so duplicate properties are rejected.
        var wrapped = "{\"mcpServers\":{" + System.Text.Json.JsonSerializer.Serialize(name) + ":" + json + "}}";
        _ = McpImportParser.Parse(wrapped, new HashSet<string>());
        return SaveAsync(name, expected, JsonNode.Parse(json)!.AsObject());
    }
    public async Task<IReadOnlyList<string>> ReadNamesAsync()
    {
        var root = await config.ReadAsync();
        if (root["mcpServers"] is null) return [];
        return root["mcpServers"] is JsonObject servers ? servers.Select(pair => pair.Key).ToArray() : throw new IOException("mcpServers must be an object.");
    }
    public async Task<JsonObject?> ReadAsync(string name)
    {
        var root = await config.ReadAsync();
        if (root["mcpServers"] is not null && root["mcpServers"] is not JsonObject) throw new IOException("mcpServers must be an object.");
        if (root["mcpServers"]?[name] is null) return null;
        return root["mcpServers"]![name] is JsonObject definition ? definition.DeepClone().AsObject() : throw new IOException("This server has an invalid definition.");
    }
    public async Task SaveAsync(string name, JsonObject? expected, JsonObject definition)
    {
        if (!(adapterAvailable?.Invoke() ?? !McpAdapterSupport.GetInstallationState(agentDirectory).NeedsSetup))
            throw new InvalidOperationException("Install or update MCP Adapter from Extensions first, then return here.");
        var json = new JsonObject { ["mcpServers"] = new JsonObject { [name] = definition.DeepClone() } }.ToJsonString();
        _ = McpImportParser.Parse(json, new HashSet<string>());
        await config.UpdateAsync(root =>
        {
            if (root["mcpServers"] is not null && root["mcpServers"] is not JsonObject) throw new IOException("mcpServers must be an object.");
            var servers = root["mcpServers"] as JsonObject ?? new JsonObject();
            if (!JsonNode.DeepEquals(servers[name], expected)) throw new IOException("This server changed in another window. Close and reopen setup to review the current settings.");
            servers[name] = definition.DeepClone();
            if (root["mcpServers"] is null) root["mcpServers"] = servers;
        });
    }
}
