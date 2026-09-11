using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>The versioned contract for the first supported Pi extension.</summary>
public static class PermissionModesSupport
{
    public const string Package = "@georgedong32/permission-modes";
    public const string Version = "2.6.3";
    public static string InstallCommand => PermissionModesCompatibility.SetupCommand;
    public const string Website = "https://github.com/GeorgeDong32/pi-permission-modes";
    public static IReadOnlyList<string> Modes { get; } = ["ask", "plan", "auto", "bypass"];
    public static string AgentDirectory => PiAgentDirectory.Resolve(Environment.GetEnvironmentVariable("PI_CODING_AGENT_DIR"),
        Environment.GetEnvironmentVariable("USERPROFILE"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static bool IsGloballyConfigured(string? agentDirectory = null)
    {
        var file = Path.Combine(agentDirectory ?? AgentDirectory, "settings.json");
        if (!File.Exists(file)) return false;
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var packages = PiJson.Field(document.RootElement, "packages");
        return packages.ValueKind == JsonValueKind.Array && packages.EnumerateArray().Any(item =>
        {
            var source = item.ValueKind == JsonValueKind.String ? item.GetString() : PiJson.Text(item, "source");
            return source == "npm:" + Package || source?.StartsWith("npm:" + Package + "@", StringComparison.Ordinal) == true;
        });
    }

    public static (string Status, bool NeedsSetup) GetInstallationState(string? agentDirectory = null)
    {
        var root = agentDirectory ?? AgentDirectory;
        if (!IsGloballyConfigured(root)) return ("Not configured globally", true);
        var manifest = Path.Combine(root, "npm", "node_modules", "@georgedong32", "permission-modes", "package.json");
        if (!File.Exists(manifest)) return ("Configured globally · package files not found", true);
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        if (PiJson.Text(document.RootElement, "name") != Package) return ("Configured globally · package identity could not be verified", true);
        var version = PiJson.Text(document.RootElement, "version");
        if (version != Version) return ($"Installed globally · {version} (supported: {Version})", true);
        return PermissionModesCompatibility.IsInstalled(Path.GetDirectoryName(manifest)!)
            ? ($"Installed globally · {version}", false)
            : ($"Installed globally · {version} · compatibility setup required", true);
    }

    public static bool IsSupportedCommand(JsonElement command)
    {
        if (PiJson.Text(command, "name") != "mode" || PiJson.Text(command, "source") != "extension") return false;
        var path = PiJson.Text(command, "path");
        var sourceInfo = PiJson.Field(command, "sourceInfo");
        if (sourceInfo.ValueKind == JsonValueKind.Object)
        {
            if (PiJson.Text(sourceInfo, "scope") != "user") return false;
            path = PiJson.Text(sourceInfo, "path");
        }
        if (!Path.IsPathFullyQualified(path)) return false;
        if (sourceInfo.ValueKind != JsonValueKind.Object)
        {
            // Older RPC versions expose only a path. Accept known user installation roots.
            var roots = new[] { AgentDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm") };
            if (!roots.Any(root => Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return false;
        }
        var manifest = Path.Combine(Path.GetDirectoryName(path)!, "package.json");
        if (!File.Exists(manifest)) return false;
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return PiJson.Text(document.RootElement, "name") == Package && PiJson.Text(document.RootElement, "version") == Version
            && PermissionModesCompatibility.IsInstalled(Path.GetDirectoryName(manifest)!);
    }

    public static string? ReadMode(JsonElement entries)
    {
        if (entries.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Pi did not return session entries.");
        string? mode = null;
        foreach (var entry in entries.EnumerateArray())
        {
            if (PiJson.Text(entry, "type") != "custom" || PiJson.Text(entry, "customType") != "modes") continue;
            var value = PiJson.Text(PiJson.Field(entry, "data"), "currentMode");
            mode = Modes.Contains(value) ? value : null;
        }
        return mode;
    }
}
