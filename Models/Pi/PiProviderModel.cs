namespace PiAgentGui.Models.Pi;

public sealed record PiProviderModel(string Id, string Name, int ContextWindow, int MaxTokens, bool Reasoning, IReadOnlyList<string> Input);
