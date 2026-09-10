namespace PiAgentGui.Models.Conversations;

/// <summary>A Pi extension question awaiting an explicit user response.</summary>
public sealed record ExtensionPrompt(string Id, string Method, string Title, string Message,
    IReadOnlyList<string> Options, string InitialValue, int? TimeoutMilliseconds);
