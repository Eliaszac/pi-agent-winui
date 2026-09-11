using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    public bool PrepareRunRevert(RunChangesViewModel summary)
    {
        if (disposed || running || busy || stopping)
        {
            ReportAttachmentError("Wait for the current operation to finish before preparing a revert request.");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(Draft) || HasPendingImages)
        {
            ReportAttachmentError("Your draft was kept. Send or clear it and its attachments before preparing a revert request.");
            return false;
        }
        var end = -1;
        for (var i = 0; i < Entries.Count; i++)
            if (ReferenceEquals(Entries[i].Summary, summary)) { end = i; break; }
        var start = end;
        while (start >= 0 && !Entries[start].IsUser) start--;
        if (start < 0 || end <= start || summary.Files.Count == 0)
        {
            ReportAttachmentError("This run's history is no longer available. Reopen the conversation and try again.");
            return false;
        }
        try
        {
            Draft = RunRevertPrompt.Build(Entries.Skip(start).Take(end - start + 1).Select(entry => entry.Source).ToArray());
            return true;
        }
        catch (ArgumentException) { ReportAttachmentError("This run is not complete yet."); return false; }
    }
}
