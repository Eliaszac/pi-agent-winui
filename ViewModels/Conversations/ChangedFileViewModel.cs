using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ChangedFileViewModel(IReadOnlyList<FileChange> changes) : ObservableObject
{
    private bool expanded;
    public bool IsExpanded { get => expanded; set => SetProperty(ref expanded, value); }
    public string Path => ProjectPathDisplay.ForTool(changes[0].Path);
    public string OpenPath => changes[0].MovedToPath ?? changes[0].Path;
    public string? MovedFrom => changes[0].Kind == "moved" ? changes[0].MovedFromPath : null;
    public string? MovedTo => changes[0].Kind == "moved" ? changes[0].MovedToPath : null;
    public string Kind => changes[0].Kind == "moved" ? "Moved" : changes[^1].Kind == "deleted" ? "Deleted" : changes[0].Kind == "created" ? "Created" : "Modified";
    public string Label => Kind == "Moved" && MovedFrom is not null && MovedTo is not null
        ? $"Moved · {ProjectPathDisplay.ForTool(MovedFrom)} → {ProjectPathDisplay.ForTool(MovedTo)}"
        : $"{Kind} · {Path}";
    public int AddedCount => changes.Sum(change => change.Added);
    public int RemovedCount => changes.Sum(change => change.Removed);
    public string Added => HasUnknownCounts ? $"+{AddedCount}*" : $"+{AddedCount}";
    public string Removed => HasUnknownCounts ? $"−{RemovedCount}*" : $"−{RemovedCount}";
    public bool HasUnknownCounts => changes.Any(change => change.Patch is null);
    public string Patch => string.Join("\n", changes.Where(change => change.Patch is not null).Select(change => change.Patch));
}
