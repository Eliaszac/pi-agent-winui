namespace PiAgentGui.Models.Conversations;

/// <summary>Reported token consumption and locally measured wall time for one settled run.</summary>
public sealed record RunUsage(long? Tokens, TimeSpan Elapsed);
