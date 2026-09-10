using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>Locates the supported global search package without executing it.</summary>
public static class PiSearchSupport
{
    public const string Package = "@heyhuynhgiabuu/pi-search";
    public const string Version = "0.3.0";
    public const string InstallCommand = "pi install npm:@heyhuynhgiabuu/pi-search@0.3.0";

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
            return source == "npm:" + Package || source?.StartsWith("npm:" + Package + "@", StringComparison.Ordinal) == true;
        })) return ("Not configured globally", true);
        var directory = Path.Combine(root, "npm", "node_modules", "@heyhuynhgiabuu", "pi-search");
        var manifest = Path.Combine(directory, "package.json");
        if (!File.Exists(manifest)) return ("Configured globally · package files not found", true);
        using var package = JsonDocument.Parse(File.ReadAllText(manifest));
        if (PiJson.Text(package.RootElement, "name") != Package) return ("Package identity could not be verified", true);
        var version = PiJson.Text(package.RootElement, "version");
        if (version != Version) return ($"Installed globally · {version} (supported: {Version})", true);
        return File.Exists(Path.Combine(directory, "dist", "index.js"))
            ? ($"Installed globally · {version}", false) : ("Installed package is incomplete", true);
    }

    public static string? FindWorkerExtension(string? agentDirectory = null)
    {
        var root = agentDirectory ?? PermissionModesSupport.AgentDirectory;
        return GetInstallationState(root).NeedsSetup ? null
            : Path.Combine(root, "npm", "node_modules", "@heyhuynhgiabuu", "pi-search", "dist", "index.js");
    }
}
