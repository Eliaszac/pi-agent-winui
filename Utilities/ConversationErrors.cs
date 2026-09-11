using System.Text.RegularExpressions;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

public static class ConversationErrors
{
    public static ErrorPresentation Describe(string message)
    {
        var lower = message.ToLowerInvariant();
        var (title, help, category) = lower switch
        {
            var text when text.Contains("acceptance is uncertain") || text.Contains("acknowledge") =>
                ("Pi didn't confirm the request", "Reconnect to check saved history before sending again. The previous request may already have run; it will not be resent automatically.", "acknowledgement-timeout"),
            var text when text.Contains("401") || text.Contains("unauthorized") || text.Contains("api key") || text.Contains("authentication") || text.Contains("credentials") =>
                ("Provider authentication failed", "Check the account or API key on the Providers page, then try again. Your unsent draft stays in this conversation.", "authentication"),
            var text when text.Contains("429") || text.Contains("rate limit") || text.Contains("quota") =>
                ("Provider limit reached", "Wait for the provider's limit to reset or choose another connected model. Check that the previous run has finished before retrying.", "provider-limit"),
            var text when text.Contains("extension") || text.Contains("cannot find module") || text.Contains("module_not_found") =>
                ("Pi extension failed", "Check Extensions for setup requirements. If a third-party package prevents startup, repair or disable that package in Pi, then reconnect.", "extension"),
            var text when text.Contains("in use by another") || text.Contains("storage") || text.Contains("access to the path") =>
                ("Session storage unavailable", "Close this session in other app windows and check access to its storage folder, then reconnect.", "storage"),
            var text when text.Contains("invalid json") || text.Contains("protocol") =>
                ("Pi connection data was invalid", "Reconnect to reload saved history. If it repeats, check Pi and extension compatibility and copy diagnostics for troubleshooting.", "protocol"),
            var text when text.Contains("pi exited") || text.Contains("connection ended") || text.Contains("connection closed") =>
                ("Pi stopped unexpectedly", "Reconnect to reopen the saved session. Check the last messages before sending again; nothing is automatically replayed.", "process-exit"),
            var text when text.Contains("timed out") || text.Contains("timeout") =>
                ("The operation timed out", "Check your connection and provider status. Review the conversation before retrying, because a timed-out operation may have been accepted.", "timeout"),
            _ => ("Couldn't finish that", "Review the error before trying again. If it keeps happening, copy diagnostics for troubleshooting.", "operation")
        };
        return new(title, Sanitize(message), help, category);
    }

    private static string Sanitize(string message)
    {
        var text = message.Length > 8000 ? message[..8000] : message;
        text = Regex.Replace(text, @"\x1B\[[0-?]*[ -/]*[@-~]", "");
        text = Regex.Replace(text, @"(?i)(https?://)[^\s/@]+:[^\s/@]+@", "$1[redacted]@");
        text = Regex.Replace(text, @"(?i)(bearer\s+)[^\s""',;]+", "$1[redacted]");
        text = Regex.Replace(text, @"(?i)((?:api[_-]?key|access[_-]?token|refresh[_-]?token|authorization|password|secret)[""']?\s*[:=]\s*[""']?)[^\s""'&,;]+", "$1[redacted]");
        foreach (var profile in new[] { Environment.GetEnvironmentVariable("USERPROFILE"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
            if (!string.IsNullOrWhiteSpace(profile))
            { text = text.Replace(profile, "~", StringComparison.OrdinalIgnoreCase).Replace(profile.Replace('\\', '/'), "~", StringComparison.OrdinalIgnoreCase); }
        return text.Length > 4000 ? text[..4000] + "\n[Error shortened]" : text;
    }
}
