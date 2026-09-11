namespace PiAgentGui.Utilities;

/// <summary>Retains the latest unhandled UI exception without recording user content.</summary>
internal static class CrashReportWriter
{
    public static void Write(Exception exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "diagnostics");
            Directory.CreateDirectory(folder);
            // Omit exception messages, which may include prompts, provider responses, or credentials.
            File.WriteAllText(Path.Combine(folder, "last-ui-crash.txt"),
                ErrorDiagnostics.Create(exception));
        }
        catch (Exception) { /* Reporting must not replace the original failure. */ }
    }
}
