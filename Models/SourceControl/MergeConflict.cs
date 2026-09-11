namespace PiAgentGui.Models.SourceControl;

public sealed record MergeConflict(string Root, string Path, string Head, string IndexEntries,
    string? Base, string? Left, string? Right, byte[]? OriginalBytes, string WorkingText, bool HasBom);
