namespace PiAgentGui.Models.Conversations;

/// <summary>A complete diagnostic response for one file, not a build or lint verdict.</summary>
public sealed record FileDiagnostics(string Path, int Errors, int Warnings);
