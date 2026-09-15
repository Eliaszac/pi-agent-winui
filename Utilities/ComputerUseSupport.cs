using System.Text.Json;

namespace PiAgentGui.Utilities;

public static class ComputerUseSupport
{
    public const string Package = "@injaneity/pi-computer-use";
    public const string Version = "0.5.1";
    public const string InstallCommand = "pi install npm:@injaneity/pi-computer-use@0.5.1";
    public static bool IsTool(string name) => name is "find_roots" or "observe_ui" or "search_ui" or "expand_ui"
        or "inspect_ui" or "act_ui" or "read_text" or "wait_for" or "launch_browser" or "navigate_browser" or "evaluate_browser";

    public static (string Status, bool NeedsSetup) GetInstallationState(string? agentDirectory = null)
    {
        var root = agentDirectory ?? PermissionModesSupport.AgentDirectory;
        var settingsPath = Path.Combine(root, "settings.json");
        if (!File.Exists(settingsPath)) return ("Not installed globally", true);
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var packages = PiJson.Field(settings.RootElement, "packages");
        if (packages.ValueKind != JsonValueKind.Array || !packages.EnumerateArray().Any(item =>
        {
            var source = item.ValueKind == JsonValueKind.String ? item.GetString() : PiJson.Text(item, "source");
            return source == "npm:" + Package || source?.StartsWith("npm:" + Package + "@", StringComparison.Ordinal) == true;
        })) return ("Not installed globally", true);
        var directory = Path.Combine(root, "npm", "node_modules", "@injaneity", "pi-computer-use");
        var manifest = Path.Combine(directory, "package.json");
        if (!File.Exists(manifest)) return ("Configured globally · package files missing", true);
        using var package = JsonDocument.Parse(File.ReadAllText(manifest));
        if (PiJson.Text(package.RootElement, "name") != Package) return ("Package identity could not be verified", true);
        var version = PiJson.Text(package.RootElement, "version");
        if (!File.Exists(Path.Combine(directory, "extensions", "computer-use.ts"))) return ("Installed package is incomplete", true);
        return ($"Installed globally · {version} · readiness checked by Pi", version != Version);
    }
}
