namespace PiAgentGui.Utilities;

public static class BrowserProfileCleanup
{
    public static void Delete(string path)
    {
        var root = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "WebView2", "Browser")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* A busy profile is retained instead of disturbing a running browser process. */ }
    }
}
