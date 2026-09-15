namespace PiAgentGui.Models.Conversations;

public sealed record PendingPrompt(string Text, string Message, IReadOnlyList<ChatImage> Images)
{
    public string Preview => (Text.Length > 240 ? Text[..240] + "…" : Text) + (Images.Count > 0 ? $" · {Images.Count} screenshot(s)" : "")
        + (Utilities.ArtifactPrompt.Read(Message).Count is > 0 and var count ? $" · {count} file(s)" : "")
        + (Utilities.GitHubReferencePrompt.Read(Message).Count is > 0 and var references ? $" · {references} GitHub reference(s)" : "");
}
