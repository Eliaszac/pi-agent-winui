using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>Checks browser extension files without starting a browser or executing extension code.</summary>
public static class PiBrowserSupport
{
    public static string? FindPackage(string? agentDirectory = null)
    {
        var root = agentDirectory ?? PermissionModesSupport.AgentDirectory;
        var candidates = new List<string> { Path.Combine(root, "extensions", "pi-browser") };
        var settingsPath = Path.Combine(root, "settings.json");
        if (File.Exists(settingsPath))
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var packages = PiJson.Field(settings.RootElement, "packages");
            if (packages.ValueKind == JsonValueKind.Array)
                foreach (var item in packages.EnumerateArray())
                    if (InstalledExtensionDiscovery.ResolvePackage(root, item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : PiJson.Text(item, "source")) is { } path) candidates.Add(path);
        }
        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "index.ts")) &&
            File.Exists(Path.Combine(path, "node_modules", "playwright", "package.json")) &&
            File.Exists(Path.Combine(path, "package.json")) && PackageName(path) == "pi-browser");
    }

    private static string PackageName(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "package.json")));
        return PiJson.Text(json.RootElement, "name");
    }
    /// <summary>Returns global installation status, including upstream's manual clone location.</summary>
    /// <param name="agentDirectory">Optional Pi agent directory for isolated checks.</param>
    /// <returns>Display status and whether installation needs attention.</returns>
    public static (string Status, bool NeedsSetup) GetInstallationState(string? agentDirectory = null)
    {
        var root = agentDirectory ?? PermissionModesSupport.AgentDirectory;
        var candidates = new List<string> { Path.Combine(root, "extensions", "pi-browser") };
        var settingsPath = Path.Combine(root, "settings.json");
        if (File.Exists(settingsPath))
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var packages = PiJson.Field(settings.RootElement, "packages");
            if (packages.ValueKind == JsonValueKind.Array)
                foreach (var item in packages.EnumerateArray())
                {
                    var source = item.ValueKind == JsonValueKind.String ? item.GetString() : PiJson.Text(item, "source");
                    if (InstalledExtensionDiscovery.ResolvePackage(root, source ?? "") is { } path)
                        candidates.Add(path);
                }
        }

        foreach (var directory in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(directory, "package.json");
            if (!File.Exists(manifest)) continue;
            using var package = JsonDocument.Parse(File.ReadAllText(manifest));
            if (PiJson.Text(package.RootElement, "name") != "pi-browser") continue;
            if (!File.Exists(Path.Combine(directory, "index.ts")) ||
                !File.Exists(Path.Combine(directory, "node_modules", "playwright", "package.json")))
                return ("Installed package is incomplete · run setup", true);
            return ($"Installed globally · {PiJson.Text(package.RootElement, "version")}", false);
        }
        return ("Not installed globally", true);
    }
}
