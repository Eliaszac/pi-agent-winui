namespace PiAgentGui.Models.Settings;

public sealed record AppPreferences(bool ResumeConversation = false, bool ShowLocalUsage = true, DateTimeOffset? UsageResetAt = null);
