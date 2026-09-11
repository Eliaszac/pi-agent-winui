namespace PiAgentGui.Models.Conversations;

/// <summary>Available distinguishes unsupported introspection from an authoritative empty inventory.</summary>
public sealed record InstructionSnapshot(bool Available, IReadOnlyList<LoadedInstruction> Files);
