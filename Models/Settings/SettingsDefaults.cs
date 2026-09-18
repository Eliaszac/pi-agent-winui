namespace PiAgentGui.Models.Settings;

/// <summary>Defines the preferences owned by each restorable category; retained user data is never reset.</summary>
public static class SettingsDefaults
{
    private static readonly AppPreferences Defaults = new();

    public static string Title(SettingsDefaultsCategory category) => category switch
    {
        SettingsDefaultsCategory.LocalUsage => "Local usage",
        _ when Enum.IsDefined(category) => category.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static string Description(SettingsDefaultsCategory category) => category switch
    {
        SettingsDefaultsCategory.Appearance => "Theme: System\nConversation text size: 15\nCode text size: 12\nResponse text weight: Semibold",
        SettingsDefaultsCategory.Conversation => "Send a message with: Enter\nCompletion sound: On",
        SettingsDefaultsCategory.Terminal => "Terminal text size: 12\nScrollback lines: 5,000\n\nApplies to open and new terminals. Reducing scrollback discards the oldest terminal output.",
        SettingsDefaultsCategory.General => "On startup: Home\nPreferred editor: System default\n\nCommand-ranking counts and recency are preserved.",
        SettingsDefaultsCategory.LocalUsage => "Show local usage on Home: On\n\nRetained usage history, totals and the last reset date are preserved. This does not reset usage totals.",
        SettingsDefaultsCategory.Updates => "Check for updates automatically: On\nDownload updates automatically: Off\n\nThis does not start an update check, download or installation, or cancel an update already in progress.",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static AppPreferences Apply(AppPreferences current, SettingsDefaultsCategory category) => category switch
    {
        SettingsDefaultsCategory.Appearance => current with
        {
            Theme = Defaults.Theme, ConversationTextSize = Defaults.ConversationTextSize,
            CodeTextSize = Defaults.CodeTextSize, ConversationTextWeight = Defaults.ConversationTextWeight
        },
        SettingsDefaultsCategory.Conversation => current with
        {
            ControlEnterToSend = Defaults.ControlEnterToSend, CompletionAudio = Defaults.CompletionAudio
        },
        SettingsDefaultsCategory.Terminal => current with
        {
            TerminalTextSize = Defaults.TerminalTextSize, TerminalScrollback = Defaults.TerminalScrollback
        },
        SettingsDefaultsCategory.General => current with { ResumeConversation = Defaults.ResumeConversation },
        SettingsDefaultsCategory.LocalUsage => current with { ShowLocalUsage = Defaults.ShowLocalUsage },
        SettingsDefaultsCategory.Updates => current with
        {
            AutomaticUpdateChecks = Defaults.AutomaticUpdateChecks, AutomaticUpdateDownloads = Defaults.AutomaticUpdateDownloads
        },
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
