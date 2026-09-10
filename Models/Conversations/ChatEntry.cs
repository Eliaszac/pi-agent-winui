namespace PiAgentGui.Models.Conversations;

/// <summary>An immutable presentation projection of a Pi message or tool execution.</summary>
public sealed record ChatEntry(string Id, string Speaker, string Text, string Details = "", string Status = "", bool IsTool = false,
    bool IsUser = false, bool IsAssistant = false, bool IsComplete = true, FileChange? FileChange = null);
