namespace PiAgentGui.Models.Conversations;

/// <summary>Latest versioned status from one conversation's optional MCP adapter.</summary>
public sealed record McpStatusSnapshot(IReadOnlyList<McpServerStatus> Servers);
