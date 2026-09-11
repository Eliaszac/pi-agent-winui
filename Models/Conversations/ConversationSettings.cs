namespace PiAgentGui.Models.Conversations;

public sealed record ConversationSettings(string? Provider = null, string? Model = null, string? Effort = null, string? ApprovalMode = null);
