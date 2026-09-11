namespace PiAgentGui.Models.Conversations;

/// <summary>Identity and content hash reported by Pi for a loaded context file.</summary>
public sealed record LoadedInstruction(string Path, string Hash);
