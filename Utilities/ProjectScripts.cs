using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Projects;

namespace PiAgentGui.Utilities;

public static class ProjectScripts
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static ProjectScriptSettings Read(JsonElement metadata)
    {
        var settings = metadata.TryGetProperty("runScripts", out var value)
            ? value.Deserialize<ProjectScriptSettings>(Options) ?? throw new InvalidDataException("Script settings are invalid.")
            : new ProjectScriptSettings();
        Validate(settings);
        return settings;
    }

    public static JsonElement Write(JsonElement metadata, ProjectScriptSettings settings)
    {
        Validate(settings);
        var node = JsonNode.Parse(metadata.GetRawText())!.AsObject();
        node["runScripts"] = JsonSerializer.SerializeToNode(settings, Options);
        return JsonSerializer.SerializeToElement(node);
    }

    public static void Validate(ProjectScriptSettings settings)
    {
        if (settings.Scripts is null || settings.Scripts.Count > 32) throw new InvalidDataException("Save up to 32 scripts per project.");
        if (settings.Scripts.Any(item => item is null)) throw new InvalidDataException("Script settings contain an invalid entry.");
        if (settings.Scripts.Select(item => item.Id).Distinct().Count() != settings.Scripts.Count)
            throw new InvalidDataException("Script identifiers must be unique.");
        foreach (var item in settings.Scripts)
        {
            if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 80)
                throw new InvalidDataException("Enter a script name of up to 80 characters.");
            if (string.IsNullOrWhiteSpace(item.Command) || item.Command.Length > 8000 || item.Command.Contains('\0'))
                throw new InvalidDataException("Enter a PowerShell command of up to 8,000 characters.");
            if (item.WorkingDirectory is null || item.WorkingDirectory.Length > 1024 || item.WorkingDirectory.Contains('\0'))
                throw new InvalidDataException("Enter a valid working directory.");
        }
        if (settings.SelectedId is { } id && !settings.Scripts.Any(item => item.Id == id))
            throw new InvalidDataException("The selected script no longer exists.");
    }

    public static string ResolveDirectory(string projectDirectory, string directory)
    {
        var resolved = string.IsNullOrWhiteSpace(directory) ? projectDirectory : Path.GetFullPath(directory.Trim(), projectDirectory);
        if (!Directory.Exists(resolved)) throw new DirectoryNotFoundException("The script's working directory does not exist.");
        return resolved;
    }
}
