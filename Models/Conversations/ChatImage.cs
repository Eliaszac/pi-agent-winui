namespace PiAgentGui.Models.Conversations;

/// <summary>An image payload persisted by Pi in the user message.</summary>
public sealed record ChatImage(string Data, string MimeType = "image/png");
