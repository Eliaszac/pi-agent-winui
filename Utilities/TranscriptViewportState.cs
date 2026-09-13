namespace PiAgentGui.Utilities;

/// <summary>Session-only reading position, independent of estimated virtualized row heights.</summary>
public sealed record TranscriptViewportState(bool FollowTail = true, string? AnchorId = null, double AnchorTop = 0);
