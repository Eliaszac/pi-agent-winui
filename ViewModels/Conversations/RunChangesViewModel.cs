using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class RunChangesViewModel : ObservableObject
{
    public string? CheckpointId { get; init; }
    public bool CanUndoRevert { get; init; }
    public string CaptureNotice { get; init; } = "";
    public bool HasCaptureNotice => CaptureNotice.Length > 0;
    public static RunChangesViewModel? FromHistory(IReadOnlyList<ChatEntry> history, string? directory = null)
    {
        var start = -1;
        for (var index = history.Count - 1; index >= 0; index--)
            if (history[index].IsUser) { start = index; break; }
        var lastResponse = history.ToList().FindLastIndex(entry => entry.IsAssistant || entry.IsTool || entry.IsUser);
        if (start < 0 || lastResponse <= start || !history[lastResponse].IsAssistant || !history[lastResponse].IsComplete) return null;
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
    public string Title => $"Changed {Files.Count} {(Files.Count == 1 ? "file" : "files")}";
    public string Added => $"+{Files.Sum(file => file.AddedCount)}";
    public string Removed => $"−{Files.Sum(file => file.RemovedCount)}";
    public bool HasUnknownCounts => Files.Any(file => file.HasUnknownCounts);
    public IReadOnlyList<string> VerificationLabels { get; }
    public bool HasVerification => VerificationLabels.Count > 0;
    public string? DiagnosticsLabel { get; }
    public bool HasDiagnostics => DiagnosticsLabel is not null;
    public IReadOnlyList<VerificationRow> VerificationRows => VerificationLabels.Select(label => new VerificationRow(label, label.Contains("failed", StringComparison.OrdinalIgnoreCase))).ToArray();

    public RunChangesViewModel(IEnumerable<FileChange> changes, IReadOnlyList<string>? verificationLabels = null, string? diagnosticsLabel = null, bool caseSensitive = false)
    {
        var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        Files = GroupMoves(changes, comparer)
            .GroupBy(change => change.Kind == "moved" ? change.Path : change.Path.Replace('\\', '/'), comparer)
            .Select(group => new ChangedFileViewModel(group.ToArray())).ToArray();
        VerificationLabels = Files.Count > 0 ? verificationLabels ?? [] : [];
        DiagnosticsLabel = Files.Count > 0 ? diagnosticsLabel : null;
    }

    private static IReadOnlyList<FileChange> GroupMoves(IEnumerable<FileChange> changes, StringComparer comparer)
    {
        var remaining = changes.ToList();
        var result = new List<FileChange>();
        foreach (var deleted in remaining.Where(change => change.Kind == "deleted" && change.BeforeHash is not null).ToArray())
        {
            var deletedName = System.IO.Path.GetFileName(deleted.Path.Replace('\\', '/'));
            var created = remaining.FirstOrDefault(change => change.Kind == "created" && change.AfterHash == deleted.BeforeHash
                && comparer.Equals(System.IO.Path.GetFileName(change.Path.Replace('\\', '/')), deletedName));
            if (created is null) continue;
            remaining.Remove(deleted);
            remaining.Remove(created);
            var from = deleted.Path.Replace('\\', '/');
            var to = created.Path.Replace('\\', '/');
            result.Add(new FileChange($"{from} -> {to}", null, 0, 0, null, "moved", MovedFromPath: from, MovedToPath: to));
        }
        result.AddRange(remaining);
        return result;
    }
}
