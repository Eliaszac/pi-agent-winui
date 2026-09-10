using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>Detects global package registration without loading MCP servers or extension code.</summary>
public static class McpAdapterSupport
{
    public static (string Status, bool NeedsSetup) GetInstallationState(string? agentDirectory = null)
    {
        var root = agentDirectory ?? PermissionModesSupport.AgentDirectory;
        var settingsPath = Path.Combine(root, "settings.json");
        if (!File.Exists(settingsPath)) return ("Not configured globally", true);
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var packages = PiJson.Field(settings.RootElement, "packages");
        if (packages.ValueKind != JsonValueKind.Array || !packages.EnumerateArray().Any(item =>
        {
            var source = item.ValueKind == JsonValueKind.String ? item.GetString() : PiJson.Text(item, "source");
            return source == "npm:pi-mcp-adapter" || source?.StartsWith("npm:pi-mcp-adapter@", StringComparison.Ordinal) == true;
        })) return ("Not configured globally", true);
        var manifest = Path.Combine(root, "npm", "node_modules", "pi-mcp-adapter", "package.json");
        if (!File.Exists(manifest)) return ("Configured globally · package files not found", true);
        using var package = JsonDocument.Parse(File.ReadAllText(manifest));
        return PiJson.Text(package.RootElement, "name") == "pi-mcp-adapter"
            ? ($"Installed globally · {PiJson.Text(package.RootElement, "version")}", false)
            : ("Package identity could not be verified", true);
    }
}
