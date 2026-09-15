namespace PiAgentGui.Models.Conversations;

public sealed record FileChange(string Path, string? Patch, int Added, int Removed, string? Unavailable, string Kind = "modified",
    string? BeforeHash = null, string? AfterHash = null, string? MovedFromPath = null, string? MovedToPath = null);
