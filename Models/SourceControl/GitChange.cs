namespace PiAgentGui.Models.SourceControl;

public sealed record GitChange(string Path, char Status, bool Staged, long? Added, long? Removed, bool Binary = false)
{
    public bool CanRevert => !Staged && Status is 'M' or 'D' or 'T' or '?';
    public bool IsConflict => Status == 'U';
    public bool IsRegularChange => !IsConflict;
    public string OpenLabel => IsConflict ? "Resolve conflict" : "View diff";
    public string Name => System.IO.Path.GetFileName(Path);
    public string Description => Status switch { '?' => "Untracked", 'A' => "Added", 'D' => "Deleted", 'U' => "Conflict", 'T' => "Type changed", _ => "Modified" };
    public string AddedText => Added is { } count ? $"+{count}" : "";
    public string RemovedText => Removed is { } count ? $"−{count}" : "";
    public string Note => Binary ? "Binary" : Added is null ? "—" : "";
    public string ActionLabel => IsConflict ? "Resolve conflict" : Staged ? "Unstage file" : "Stage file";
    public string ActionGlyph => IsConflict ? "\uE90F" : Staged ? "\uE738" : "\uE710";
}
