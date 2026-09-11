namespace PiAgentGui.Models.Conversations;

public sealed record SplitDiffRows(IReadOnlyList<DiffLine> Left, IReadOnlyList<DiffLine> Right);
