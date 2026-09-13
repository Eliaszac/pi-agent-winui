using PiAgentGui.Models.Conversations;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>Groups consecutive tool calls without altering the underlying transcript.</summary>
public sealed class TranscriptPresentation
{
    private readonly Dictionary<string, ChatEntryViewModel> groups = [];
    private readonly ChatEntryViewModel processing = new(new ChatEntry("presentation:processing", "", "")) { IsProcessing = true };

    public void UpdateProcessingLabel(string label) => processing.Update(new ChatEntry("presentation:processing", "", label));

    public IReadOnlyList<ChatEntryViewModel> Project(IEnumerable<ChatEntryViewModel> entries, bool isRunning = false, string processingLabel = "Processing…")
    {
        var rows = new List<ChatEntryViewModel>();
        var run = new List<ChatEntryViewModel>();
        var activeGroups = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (entry.IsEmptyAssistant) continue;
            if (entry.IsTool) { run.Add(entry); continue; }
            Flush(run, rows, activeGroups);
            rows.Add(entry);
        }
        Flush(run, rows, activeGroups);
        if (isRunning)
        {
            processing.Update(new ChatEntry("presentation:processing", "", processingLabel));
            rows.Insert(rows.FindLastIndex(entry => entry.IsUser) + 1, processing);
        }
        foreach (var key in groups.Keys.Where(key => !activeGroups.Contains(key)).ToArray()) groups.Remove(key);
        return rows;
    }

    private void Flush(List<ChatEntryViewModel> run, List<ChatEntryViewModel> rows, HashSet<string> activeGroups)
    {
        if (run.Count > 1)
        {
            var id = run[0].Id;
            activeGroups.Add(id);
            if (!groups.TryGetValue(id, out var group)) groups[id] = group = new(new ChatEntry("group:" + id, "", ""));
            group.SetTools(run.ToArray());
            rows.Add(group);
        }
        else rows.AddRange(run);
        run.Clear();
    }
}
