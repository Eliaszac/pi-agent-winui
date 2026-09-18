using System.Globalization;
using System.Runtime.InteropServices;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Settings;

namespace PiAgentGui.Services.Settings;

/// <summary>Builds a local, allowlisted report without reading logs, sessions, credentials or filesystem paths.</summary>
public static class SettingsDiagnosticSummary
{
    public static string Create(AppPreferences preferences)
    {
        var settings = preferences.Normalize();
        return string.Join(Environment.NewLine,
            "Pi desktop diagnostic summary (format 1)",
            "",
            "Application",
            "Version: " + ApplicationIdentity.Version,
            "Windows version: " + Environment.OSVersion.Version,
            "OS architecture: " + RuntimeInformation.OSArchitecture,
            "Process architecture: " + RuntimeInformation.ProcessArchitecture,
            ".NET runtime: " + Environment.Version,
            "",
            "Desktop preferences",
            "Theme: " + (settings.Theme switch { 1 => "Light", 2 => "Dark", _ => "System" }),
            "Conversation text size: " + settings.ConversationTextSize.ToString(CultureInfo.InvariantCulture),
            "Code text size: " + settings.CodeTextSize.ToString(CultureInfo.InvariantCulture),
            "Response text weight: " + (settings.ConversationTextWeight switch { 400 => "Regular", 500 => "Medium", _ => "Semibold" }),
            "Send shortcut: " + (settings.ControlEnterToSend ? "Ctrl + Enter" : "Enter"),
            "Completion sound: " + OnOff(settings.CompletionAudio),
            "Terminal text size: " + settings.TerminalTextSize.ToString(CultureInfo.InvariantCulture),
            "Terminal scrollback lines: " + settings.TerminalScrollback.ToString(CultureInfo.InvariantCulture),
            "Startup page: " + (settings.ResumeConversation ? "Most recent conversation" : "Home"),
            "Show local usage: " + OnOff(settings.ShowLocalUsage),
            "Automatic update checks: " + OnOff(settings.AutomaticUpdateChecks),
            "Automatic update downloads: " + OnOff(settings.AutomaticUpdateDownloads),
            "",
            "Scope: desktop environment and preferences only. Pi and remote targets were not probed.",
            "Excluded: credentials, conversations, project details, paths, usage history and logs.");
    }

    private static string OnOff(bool enabled) => enabled ? "On" : "Off";
}
