using System.Text.Json;

namespace PiAgentGui.Utilities;

public static class AutoSessionNameSupport
{
    public const string InstallCommand = "pi install npm:pi-auto-session-name@0.1.1";
    public static (string Status, bool NeedsSetup) GetInstallationState()
    {
        var root = PermissionModesSupport.AgentDirectory;
        var settingsPath = Path.Combine(root, "settings.json");
        if (!File.Exists(settingsPath)) return ("Not configured globally", true);
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var packages = PiJson.Field(settings.RootElement, "packages");
        if (packages.ValueKind != JsonValueKind.Array || !packages.EnumerateArray().Any(item =>
        {
            var source = item.ValueKind == JsonValueKind.String ? item.GetString() : PiJson.Text(item, "source");
            return source == "npm:pi-auto-session-name" || source?.StartsWith("npm:pi-auto-session-name@", StringComparison.Ordinal) == true;
        })) return ("Not configured globally", true);
        var manifest = Path.Combine(root, "npm", "node_modules", "pi-auto-session-name", "package.json");
        if (!File.Exists(manifest)) return ("Configured globally · package files not found", true);
        using var package = JsonDocument.Parse(File.ReadAllText(manifest));
        return PiJson.Text(package.RootElement, "name") == "pi-auto-session-name"
            ? ($"Installed globally · {PiJson.Text(package.RootElement, "version")}", false) : ("Package identity could not be verified", true);
    }
}
