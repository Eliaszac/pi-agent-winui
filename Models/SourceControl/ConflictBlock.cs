namespace PiAgentGui.Models.SourceControl;

public sealed record ConflictBlock(int Start, int Length, string Left, string Right, string? Base);
