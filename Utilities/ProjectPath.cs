namespace PiAgentGui.Utilities;

/// <summary>Normalizes Windows project directory paths without resolving filesystem aliases.</summary>
public static class ProjectPath
{
    /// <summary>Normalizes a fully qualified directory, preserving a volume root.</summary>
    /// <param name="path">The absolute directory path.</param>
    /// <returns>A full path without an unnecessary trailing separator.</returns>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Choose an absolute project directory.", nameof(path));

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
