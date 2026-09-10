namespace PiAgentGui.Utilities;

/// <summary>Formats project locations without exposing parent directories or network hosts.</summary>
public static class ProjectPathDisplay
{
    public static string ForTool(string path) => Path.IsPathFullyQualified(path) ? Redact(path) : path;
    public static string Redact(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var normalized = path.Replace('/', '\\').TrimEnd('\\');
        var leaf = normalized[(normalized.LastIndexOf('\\') + 1)..];
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal)) return $"Network\\…\\{leaf}";
        if (normalized.Length >= 2 && normalized[1] == ':')
            return normalized.Length == 2 ? normalized + "\\" : $"{normalized[..2]}\\…\\{leaf}";
        return $"…\\{leaf}";
    }
}
