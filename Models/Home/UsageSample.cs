namespace PiAgentGui.Models.Home;

/// <summary>Usage metadata only; never retains message content, tool output or credentials.</summary>
public sealed record UsageSample(string Key, Guid ProjectId, Guid ConversationId, DateTimeOffset At, string Provider, string Model, long? Tokens, string? Effort = null);
