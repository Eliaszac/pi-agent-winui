using System.Text.Json;
using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Utilities;

/// <summary>Reads global extension metadata without importing or executing extension code.</summary>
public sealed class InstalledExtensionDiscovery
{
    public IReadOnlyList<InstalledExtension> Discover(string? agentDirectory = null)
    {
        var root = agentDirectory ?? PermissionModesSupport.AgentDirectory;
        var results = new Dictionary<string, InstalledExtension>(StringComparer.OrdinalIgnoreCase);
        var settingsPath = Path.Combine(root, "settings.json");
        if (File.Exists(settingsPath))
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var packages = PiJson.Field(settings.RootElement, "packages");
            if (packages.ValueKind == JsonValueKind.Array)
                foreach (var package in packages.EnumerateArray())
                {
                    var source = package.ValueKind == JsonValueKind.String ? package.GetString()! : PiJson.Text(package, "source");
                    var path = ResolvePackage(root, source);
                    if (path is not null) Add(path, source.StartsWith("npm:", StringComparison.Ordinal) ? "npm package" : "Local / Git package", false, results);
                }
            var extensions = PiJson.Field(settings.RootElement, "extensions");
            if (extensions.ValueKind == JsonValueKind.Array)
                foreach (var extension in extensions.EnumerateArray())
                    if (extension.ValueKind == JsonValueKind.String && extension.GetString() is { Length: > 0 } path)
                    {
                        // Exclusion entries are configuration rules, not installed resources.
                        if (path.StartsWith('!') || path.StartsWith('-')) continue;
                        Add(ResolveLocal(root, path), "Configured local extension", true, results);
                    }
        }
        var local = Path.Combine(root, "extensions");
        if (Directory.Exists(local))
        {
            foreach (var path in Directory.EnumerateFiles(local).Where(IsExtensionFile)) Add(path, "Global extension file", true, results);
            foreach (var path in Directory.EnumerateDirectories(local)) Add(path, "Global extension directory", true, results);
        }
        return results.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Add(string path, string origin, bool direct, Dictionary<string, InstalledExtension> results)
    {
        path = Path.GetFullPath(path);
        if (results.ContainsKey(path)) return;
        if (File.Exists(path))
        {
            if (IsExtensionFile(path)) results[path] = new(Path.GetFileNameWithoutExtension(path), "", "Local extension · metadata unavailable", origin);
            return;
        }
        if (!Directory.Exists(path)) return;
        var manifest = Path.Combine(path, "package.json");
        var hasEntry = File.Exists(Path.Combine(path, "index.ts")) || File.Exists(Path.Combine(path, "index.js"));
        var hasFolder = Directory.Exists(Path.Combine(path, "extensions"));
        if (!File.Exists(manifest))
        {
            if (hasEntry || hasFolder) results[path] = new(Path.GetFileName(path), "", "Extension directory · metadata unavailable", origin);
            return;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            var metadata = document.RootElement;
            var name = PiJson.Text(metadata, "name");
            if (name is "@georgedong32/permission-modes" or "pi-auto-session-name" or "pi-mcp-adapter" or "@heyhuynhgiabuu/pi-search" or "lsp-pi" or "pi-browser" or ComputerUseSupport.Package) return;
            var declared = PiJson.Field(PiJson.Field(metadata, "pi"), "extensions");
            if (!(declared.ValueKind == JsonValueKind.Array && declared.GetArrayLength() > 0) && !hasFolder && !(direct && hasEntry)) return;
            var description = PiJson.Text(metadata, "description");
            results[path] = new(name.Length > 0 ? name : Path.GetFileName(path), PiJson.Text(metadata, "version"),
                description.Length > 0 ? description : "No description provided by the extension.", origin);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            if (hasEntry || hasFolder) results[path] = new(Path.GetFileName(path), "", "Extension metadata could not be read", origin);
        }
    }

    private static bool IsExtensionFile(string path) => Path.GetExtension(path) is ".ts" or ".js" && !path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase);

    private static string ResolveLocal(string root, string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return Path.GetFullPath(path, root);
    }

    internal static string? ResolvePackage(string root, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        if (source.StartsWith("npm:", StringComparison.Ordinal))
        {
            var name = source[4..];
            var version = name.IndexOf('@', 1);
            if (version >= 0) name = name[..version];
            if (name.Contains("..", StringComparison.Ordinal) || name.Contains('\\')) return null;
            return Path.Combine(root, "npm", "node_modules", name.Replace('/', Path.DirectorySeparatorChar));
        }
        if (source.StartsWith("git:", StringComparison.Ordinal) || source.StartsWith("https://", StringComparison.Ordinal) || source.StartsWith("ssh://", StringComparison.Ordinal))
        {
            var remote = source.StartsWith("git:", StringComparison.Ordinal) ? source[4..] : source;
            if (remote.StartsWith("git@", StringComparison.Ordinal)) remote = remote[4..].Replace(':', '/');
            if (!remote.Contains("://", StringComparison.Ordinal)) remote = "https://" + remote;
            if (!Uri.TryCreate(remote, UriKind.Absolute, out var uri)) return null;
            var repo = uri.AbsolutePath.Trim('/');
            var reference = repo.IndexOf('@');
            if (reference >= 0) repo = repo[..reference];
            if (repo.EndsWith(".git", StringComparison.Ordinal)) repo = repo[..^4];
            return Path.Combine(root, "git", uri.Host, repo.Replace('/', Path.DirectorySeparatorChar));
        }
        return ResolveLocal(root, source);
    }
}
