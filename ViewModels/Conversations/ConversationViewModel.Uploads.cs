using System.Collections.ObjectModel;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    public ObservableCollection<ArtifactItemViewModel> PendingFiles { get; } = [];
    public bool HasPendingFiles => PendingFiles.Count > 0;
    private bool uploading;
    public bool IsUploading => uploading;
    public bool CanAttachFiles => Artifacts is not null && !uploading && !disposed;

    public void AttachArtifact(ArtifactItemViewModel item)
    {
        if (disposed || !ReferenceEquals(item.Owner, Artifacts)) throw new IOException("This conversation is no longer available.");
        if (!item.Available) throw new IOException("This artifact is unavailable. Remove it from the message or choose another file.");
        if (PendingFiles.Contains(item)) return;
        if (PendingFiles.Count >= ArtifactPrompt.MaximumFiles) throw new IOException("Attach up to eight files per message.");
        PendingFiles.Add(item);
        RefreshAttachments();
    }
    public async Task RemoveFileAsync(ArtifactItemViewModel item)
    {
        if (!PendingFiles.Remove(item)) return;
        RefreshAttachments();
        if (Artifacts is not { } owner || busy || dispatchingQueue
            || queued is { } pending && ArtifactPrompt.Read(pending.Message).Contains(item.Id)
            || RecoveredPrompts.Any(prompt => ArtifactPrompt.Read(prompt.Message).Contains(item.Id))) return;
        var operation = owner.Store.DiscardUploadAsync(item.Id);
        TrackArtifactOperation(operation);
        await operation;
        await owner.RefreshAsync();
    }

    public async Task UploadFilesAsync(IReadOnlyList<string> paths)
    {
        if (!CanAttachFiles || Artifacts is not { } owner) return;
        if (paths.Count + PendingFiles.Count > ArtifactPrompt.MaximumFiles) throw new IOException("Attach up to eight files per message.");
        uploading = true;
        RefreshAttachments();
        var operation = UploadBatchAsync(owner, paths);
        TrackArtifactOperation(operation);
        try { await operation; }
        finally { uploading = false; RefreshAttachments(); }
    }
    private async Task UploadBatchAsync(ArtifactPanelViewModel owner, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            var item = await owner.UploadAsync(path);
            if (!disposed) AttachArtifact(item);
        }
    }
    private string ExpandAttachments(string text)
    {
        if (PendingFiles.Any(file => !file.Available)) throw new IOException("An attached artifact was deleted. Remove it from this message before sending.");
        return ArtifactPrompt.Append(FileReferences.Expand(text), PendingFiles.Select(file => file.Record));
    }
    private async Task ShareMessageArtifactsAsync(string message)
    {
        var ids = ArtifactPrompt.Read(message);
        if (ids.Count == 0 || Artifacts is not { } artifacts) return;
        // Sending runs off the UI thread. Publish only storage state here, then refresh bindings through the dispatcher.
        await artifacts.ShareAsync(ids, refresh: false).ConfigureAwait(false);
        dispatcher.Post(() => TrackArtifactOperation(artifacts.RefreshAsync()));
    }
    private void RestoreFiles(string message)
    {
        if (Artifacts is not { } artifacts) return;
        foreach (var id in ArtifactPrompt.Read(message)) PendingFiles.Add(artifacts.Get(id));
        TrackArtifactOperation(artifacts.RefreshAsync());
    }
    private void BindUserArtifacts(ChatEntryViewModel entry)
    {
        if (!entry.IsUser || Artifacts is not { } artifacts) return;
        entry.AttachedFiles = ArtifactPrompt.Read(entry.Source.Text).Select(artifacts.Get).ToArray();
        if (entry.AttachedFiles.Count > 0) refreshArtifacts = true;
    }
}
