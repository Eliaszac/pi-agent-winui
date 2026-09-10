using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Retains only successful checks after the most recent potentially mutating tool.</summary>
public sealed class RunVerificationTracker
{
    private readonly Dictionary<string, (int Revision, string Kind)> started = [];
    private readonly HashSet<string> completed = [];
    private int revision;
    private bool lint;
    private string? tests;
    private readonly Dictionary<string, FileDiagnostics> diagnostics = new(StringComparer.OrdinalIgnoreCase);

    public string? DiagnosticsLabel(IEnumerable<FileChange> changes, string? directory)
    {
        try
        {
            var changed = changes.Select(file => Normalize(file.Path, directory)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matching = diagnostics.Values.GroupBy(file => Normalize(file.Path, directory), StringComparer.OrdinalIgnoreCase)
                .Where(group => changed.Contains(group.Key)).Select(group => group.Last()).ToArray();
            if (matching.Length == 0) return null;
            var errors = matching.Sum(file => file.Errors); var warnings = matching.Sum(file => file.Warnings);
            return $"Diagnostics · {errors} {(errors == 1 ? "error" : "errors")} · {warnings} {(warnings == 1 ? "warning" : "warnings")} · {matching.Length}/{changed.Count} files checked";
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private static string Normalize(string path, string? directory) =>
        Path.GetFullPath(path, directory ?? Environment.CurrentDirectory).Replace('\\', '/');

    public IReadOnlyList<string> Labels => (lint ? new[] { "Lint passed" } : Array.Empty<string>())
        .Concat(tests is null ? [] : new[] { tests }).ToArray();

    public void Reset() { started.Clear(); completed.Clear(); revision = 0; lint = false; tests = null; diagnostics.Clear(); }

    public void Observe(ChatEntry entry)
    {
        if (!entry.IsTool || completed.Contains(entry.Id)) return;
        var kind = VerificationOutputParser.Kind(entry.Speaker, entry.Details);
        var mayChangeFiles = entry.FileChange is not null || entry.Speaker is "write" or "edit" || entry.Speaker == "bash" && kind.Length == 0;
        if (!started.ContainsKey(entry.Id))
        {
            if (mayChangeFiles) Invalidate();
            started[entry.Id] = (revision, kind);
            if (kind == "lint") lint = false;
            if (kind == "tests") tests = null;
            if (entry.Speaker == "lsp") diagnostics.Clear();
        }
        if (entry.Status == "Running") return;
        completed.Add(entry.Id);
        // A write overlapping another tool invalidates checks even when it finishes last.
        if (mayChangeFiles) { Invalidate(); return; }
        var check = started[entry.Id];
        if (check.Kind == "lint") lint = false;
        if (check.Kind == "tests") tests = null;
        if (entry.Status != "Completed" || check.Revision != revision) return;
        foreach (var file in entry.Diagnostics ?? []) diagnostics[file.Path] = file;
        if (check.Kind == "lint") lint = true;
        if (check.Kind == "tests") tests = VerificationOutputParser.PassedTests(entry.Text);
    }

    private void Invalidate() { revision++; lint = false; tests = null; diagnostics.Clear(); }
}
