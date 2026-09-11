namespace PiAgentGui.Models.Conversations;

public sealed record UnifiedDiffDocument(string FileName, IReadOnlyList<DiffLine> Lines, string Notice);
