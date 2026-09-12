using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ChangedFileViewModel(IReadOnlyList<FileChange> changes)
{
    public string Path => ProjectPathDisplay.ForTool(changes[0].Path);
    public string Kind => changes[^1].Kind == "deleted" ? "Deleted" : changes[0].Kind == "created" ? "Created" : "Modified";
    public string Label => $"{Kind} · {Path}";
    public int AddedCount => changes.Sum(change => change.Added);
    public int RemovedCount => changes.Sum(change => change.Removed);
    public string Added => HasUnknownCounts ? $"+{AddedCount}*" : $"+{AddedCount}";
    public string Removed => HasUnknownCounts ? $"−{RemovedCount}*" : $"−{RemovedCount}";
    public bool HasUnknownCounts => changes.Any(change => change.Patch is null);
    public string Patch => string.Join("\n", changes.Where(change => change.Patch is not null).Select(change => change.Patch));
}
