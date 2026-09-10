using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class RunChangesViewModel : ObservableObject
{
    public static RunChangesViewModel? FromHistory(IReadOnlyList<ChatEntry> history, string? directory = null)
    {
        var start = -1;
        for (var index = history.Count - 1; index >= 0; index--)
            if (history[index].IsUser) { start = index; break; }
        if (start < 0 || history.Count <= start + 1 || !history[^1].IsAssistant || !history[^1].IsComplete) return null;
        var run = history.Skip(start + 1).ToArray();
        if (run.Any(entry => !entry.IsComplete)) return null;
        var verification = new RunVerificationTracker();
        foreach (var entry in run) verification.Observe(entry);
        var changes = run.Select(entry => entry.FileChange).OfType<FileChange>().ToArray();
        return new(changes, verification.Labels, verification.DiagnosticsLabel(changes, directory));
    }

    private bool expanded;
    public IReadOnlyList<ChangedFileViewModel> Files { get; }
    public IReadOnlyList<ChangedFileViewModel> VisibleFiles => expanded ? Files : Files.Take(3).ToArray();
    public bool HasMore => Files.Count > 3;
    public string MoreLabel => expanded ? "Show fewer files" : $"Show {Files.Count - 3} more files";
    public bool IsExpanded
    {
        get => expanded;
        set
        {
            if (!SetProperty(ref expanded, value)) return;
            OnPropertyChanged(nameof(VisibleFiles));
            OnPropertyChanged(nameof(MoreLabel));
        }
    }
    public string Title => $"Edited {Files.Count} {(Files.Count == 1 ? "file" : "files")}";
    public string Added => $"+{Files.Sum(file => file.AddedCount)}";
    public string Removed => $"−{Files.Sum(file => file.RemovedCount)}";
    public bool HasUnknownCounts => Files.Any(file => file.HasUnknownCounts);
    public IReadOnlyList<string> VerificationLabels { get; }
    public bool HasVerification => VerificationLabels.Count > 0;
    public string? DiagnosticsLabel { get; }
    public bool HasDiagnostics => DiagnosticsLabel is not null;

    public RunChangesViewModel(IEnumerable<FileChange> changes, IReadOnlyList<string>? verificationLabels = null, string? diagnosticsLabel = null)
    {
        Files = changes.GroupBy(change => change.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChangedFileViewModel(group.ToArray())).ToArray();
        VerificationLabels = Files.Count > 0 ? verificationLabels ?? [] : [];
        DiagnosticsLabel = Files.Count > 0 ? diagnosticsLabel : null;
    }
}
