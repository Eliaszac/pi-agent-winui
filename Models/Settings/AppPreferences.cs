namespace PiAgentGui.Models.Settings;

public sealed record AppPreferences(bool ResumeConversation = false, bool ShowLocalUsage = true, DateTimeOffset? UsageResetAt = null,
    int Theme = 0, double ConversationTextSize = 15, double CodeTextSize = 12, bool ControlEnterToSend = false, int ConversationTextWeight = 400)
{
    public AppPreferences Normalize() => this with
    {
        Theme = Theme is >= 0 and <= 2 ? Theme : 0,
        ConversationTextWeight = ConversationTextWeight is 400 or 500 or 600 ? ConversationTextWeight : 400,
        ConversationTextSize = double.IsFinite(ConversationTextSize) ? Math.Clamp(ConversationTextSize, 12, 22) : 15,
        CodeTextSize = double.IsFinite(CodeTextSize) ? Math.Clamp(CodeTextSize, 10, 20) : 12
    };
}
