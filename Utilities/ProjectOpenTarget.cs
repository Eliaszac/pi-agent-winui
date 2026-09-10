namespace PiAgentGui.Utilities;

public static class ProjectOpenTarget
{
    public static string? FindSolution(string directory)
    {
        try
        {
            var files = Directory.EnumerateFiles(directory).Where(file =>
                Path.GetExtension(file).Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(file).Equals(".slnx", StringComparison.OrdinalIgnoreCase)).Take(3).ToArray();
            if (files.Length == 1) return files[0];
            // A matching .sln/.slnx pair represents the same named solution; prefer the modern format.
            if (files.Length == 2 && Path.GetFileNameWithoutExtension(files[0]).Equals(Path.GetFileNameWithoutExtension(files[1]), StringComparison.OrdinalIgnoreCase))
                return files.First(file => Path.GetExtension(file).Equals(".slnx", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return null;
    }
}
