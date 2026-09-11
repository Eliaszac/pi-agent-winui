namespace PiAgentGui.Models.Conversations;

/// <summary>An editable disk snapshot with a conflict token and the original UTF-8 format.</summary>
public sealed record InstructionDocument(string Path, string Text, string DiskHash, string ContentHash, bool HasBom, string NewLine);
