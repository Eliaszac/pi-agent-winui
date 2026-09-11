using System.Text.Json;
using PiAgentGui.Models.Projects;

namespace PiAgentGui.Utilities;

/// <summary>Reads package scripts without executing package-manager commands.</summary>
public static class PackageScriptImporter
{
    public static async Task<IReadOnlyList<PackageScriptCandidate>> ReadAsync(string file, string projectDirectory)
    {
        if (!string.Equals(Path.GetFileName(file), "package.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a package.json file.");
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > 1024 * 1024) throw new InvalidDataException("Choose a package.json smaller than 1 MB.");
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("package.json must contain an object.");
        if (!root.TryGetProperty("scripts", out var scripts)) return [];
        if (scripts.ValueKind != JsonValueKind.Object) throw new InvalidDataException("The scripts field must contain named commands.");
        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(file))!;
        var manager = DetectManager(root, packageDirectory);
        var relative = Path.GetRelativePath(projectDirectory, packageDirectory);
        var directory = relative == "." ? "" : relative;
        var result = new List<PackageScriptCandidate>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in scripts.EnumerateObject())
        {
            if (result.Count >= 256) throw new InvalidDataException("Preview supports up to 256 package scripts.");
            if (!names.Add(item.Name)) throw new InvalidDataException("package.json contains duplicate script names.");
            if (item.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.Value.GetString()))
                throw new InvalidDataException("Each package script must contain a non-empty command.");
            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 80 || item.Name.StartsWith('-') || item.Name.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or ':' or '.')))
                throw new InvalidDataException("Imported script names must be 1–80 letters, digits, dots, colons, underscores or dashes, and cannot start with a dash. Add unusual names manually.");
            var script = new ProjectScript(Guid.NewGuid(), item.Name, manager + " run '" + item.Name.Replace("'", "''") + "'", directory);
            result.Add(new(script, item.Value.GetString()!));
        }
        return result;
    }

    private static string DetectManager(JsonElement root, string directory)
    {
        if (root.TryGetProperty("packageManager", out var value) && value.ValueKind == JsonValueKind.String)
        {
            var declared = value.GetString()!.Split('@')[0];
            if (declared is "npm" or "pnpm" or "yarn" or "bun") return declared;
        }
        if (File.Exists(Path.Combine(directory, "pnpm-lock.yaml"))) return "pnpm";
        if (File.Exists(Path.Combine(directory, "yarn.lock"))) return "yarn";
        if (File.Exists(Path.Combine(directory, "bun.lock")) || File.Exists(Path.Combine(directory, "bun.lockb"))) return "bun";
        return "npm";
    }

    public static IReadOnlyList<ProjectScript> Merge(string projectDirectory, IReadOnlyList<ProjectScript> existing, IReadOnlyList<ProjectScript> selected)
    {
        var result = existing.ToList();
        foreach (var script in selected)
        {
            var directory = ProjectScripts.ResolveDirectory(projectDirectory, script.WorkingDirectory);
            if (result.Any(item => item.Command == script.Command &&
                string.Equals(Path.GetFullPath(string.IsNullOrWhiteSpace(item.WorkingDirectory) ? "." : item.WorkingDirectory, projectDirectory), directory, StringComparison.OrdinalIgnoreCase))) continue;
            var label = script.Name;
            for (var suffix = 2; result.Any(item => string.Equals(item.Name, label, StringComparison.OrdinalIgnoreCase)); suffix++)
            {
                var ending = $" ({suffix})";
                label = script.Name[..Math.Min(script.Name.Length, 80 - ending.Length)] + ending;
            }
            result.Add(script with { Id = Guid.NewGuid(), Name = label });
        }
        ProjectScripts.Validate(new() { Scripts = result });
        return result;
    }
}
