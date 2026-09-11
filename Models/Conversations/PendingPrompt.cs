namespace PiAgentGui.Models.Conversations;

public sealed record PendingPrompt(string Text, string Message, IReadOnlyList<ChatImage> Images)
{
    public string Preview => (Text.Length > 240 ? Text[..240] + "…" : Text) + (Images.Count > 0 ? $" · {Images.Count} screenshot(s)" : "");
}
