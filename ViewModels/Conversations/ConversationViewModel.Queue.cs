using System.Collections.ObjectModel;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    private PendingPrompt? queued;
    private bool queueHeld;
    private bool queueReady;
    private bool dispatchingQueue;
    private int steeringCount;
    private ChatEntryViewModel? submittedPreview;

    private void ShowSubmittedPreview(string text, IReadOnlyList<ChatImage> images)
    {
        if (running || text.TrimStart().StartsWith('/')) return;
        submittedPreview = new(new ChatEntry("presentation:submitted", "You", text, IsUser: true, Images: images));
        BindUserArtifacts(submittedPreview);
        SynchronizeProcessingTime();
        RefreshTranscript();
    }
    public ObservableCollection<PendingPrompt> RecoveredPrompts { get; } = [];
    public bool HasQueuedPrompt => queued is not null;
    public string QueuedPreview => queued?.Preview ?? "";
    public string QueueLabel => queueHeld ? "Follow-up paused" : "Queued follow-up";
    public bool CanChangeQueue => !busy && !stopping && !dispatchingQueue;
    public bool CanReleaseQueue => CanChangeQueue && IsReady && queued is not null && queueHeld;
    public bool CanSteerQueue => CanChangeQueue && IsReady && running && queued is not null;
    public bool HasSteering => steeringCount > 0;
    public string SteeringLabel => $"{steeringCount} steering message(s) pending";
    public bool IsSteeringDraft => SteeringCommand.Matches(Draft);
    public AsyncRelayCommand EditQueuedCommand { get; private set; } = null!;
    public AsyncRelayCommand RemoveQueuedCommand { get; private set; } = null!;
    public AsyncRelayCommand ReleaseQueuedCommand { get; private set; } = null!;
    public AsyncRelayCommand SteerQueuedCommand { get; private set; } = null!;
    public AsyncRelayCommand RestoreRecoveredCommand { get; private set; } = null!;

    private void InitializeQueue()
    {
        EditQueuedCommand = new(_ =>
        {
            if (CanChangeQueue && queued is { } item) { RestorePrompt(item); queued = null; NotifyQueue(); }
            return Task.CompletedTask;
        }, ReportError);
        RemoveQueuedCommand = new(_ => { if (CanChangeQueue) { queued = null; NotifyQueue(); } return Task.CompletedTask; }, ReportError);
        ReleaseQueuedCommand = new(_ =>
        {
            if (CanReleaseQueue) { SetError(""); queueHeld = false; queueReady = !running; NotifyQueue(); TryDispatchQueued(); }
            return Task.CompletedTask;
        }, ReportError);
        SteerQueuedCommand = new(async _ =>
        {
            if (!CanSteerQueue || queued is not { } item) return;
            queueHeld = true;
            await ExecuteAsync(async () => { await ShareMessageArtifactsAsync(item.Message); await session.SteerAsync(item.Message, item.Images); });
            if (ReferenceEquals(queued, item)) queued = null;
            NotifyQueue();
        }, ReportError);
        RestoreRecoveredCommand = new(value =>
        {
            if (value is PendingPrompt item) { RestorePrompt(item); RecoveredPrompts.Remove(item); }
            return Task.CompletedTask;
        }, ReportError);
    }

    private void RestorePrompt(PendingPrompt item)
    {
        if (!string.IsNullOrWhiteSpace(Draft) || HasPendingImages || HasPendingFiles || HasPendingGitHub)
            throw new InvalidOperationException("Clear the composer before restoring this message. Your pending message has been kept.");
        Draft = item.Text;
        foreach (var image in item.Images) PendingImages.Add(image);
        RestoreFiles(item.Message);
        foreach (var reference in GitHubReferencePrompt.Read(item.Message)) AttachGitHub(reference);
        RefreshAttachments();
    }

    private async Task<bool> HandlePendingSendAsync(string submitted, ChatImage[] images)
    {
        if (SteeringCommand.Matches(submitted))
        {
            var message = submitted.TrimStart()[6..].TrimStart();
            if (string.IsNullOrWhiteSpace(message) && images.Length == 0 && !HasPendingFiles && !HasPendingGitHub)
                throw new InvalidOperationException("Add a message after /steer.");
            if (message.StartsWith('/')) throw new InvalidOperationException("Use /steer with a message, rather than another slash command.");
            var steer = running;
            var expanded = await ExpandAttachmentsAsync(message);
            await SendSubmittedAsync(submitted, expanded, images, () => steer
                ? session.SteerAsync(expanded, images)
                : session.SendAsync(expanded, images));
            return true;
        }
        if (!running) return false;
        if (submitted.TrimStart().StartsWith('/'))
            throw new InvalidOperationException("While running, use /steer for a correction or send a message to queue a follow-up. Other commands are available when the run finishes.");
        if (queued is not null)
            throw new InvalidOperationException("One follow-up is already queued. Edit or remove it first, or use /steer for a correction. Your draft has been kept.");
        queued = new(submitted, await ExpandAttachmentsAsync(submitted), images);
        queueHeld = false;
        queueReady = false;
        SetError("");
        ClearSubmitted(submitted, images, queued.Message);
        NotifyQueue();
        return true;
    }

    private void ClearSubmitted(string submitted, IReadOnlyList<ChatImage> images, string expanded)
    {
        if (Draft == submitted) Draft = "";
        var references = GitHubReferencePrompt.Read(expanded).Select(item => item.Url).ToHashSet();
        foreach (var reference in PendingGitHub.Where(item => references.Contains(item.Url)).ToArray()) PendingGitHub.Remove(reference);
        foreach (var image in images) PendingImages.Remove(image);
        var fileIds = ArtifactPrompt.Read(expanded);
        foreach (var file in PendingFiles.Where(file => fileIds.Contains(file.Id)).ToArray()) PendingFiles.Remove(file);
        RefreshAttachments();
    }

    private async Task SendSubmittedAsync(string submitted, string expanded, ChatImage[] images, Func<Task> send)
    {
        if (busy || disposed) return;
        ClearSubmitted(submitted, images, expanded);
        ShowSubmittedPreview(expanded, images);
        try { await ExecuteAsync(async () => { await ShareMessageArtifactsAsync(expanded); await send(); }); }
        catch
        {
            submittedPreview = null;
            RefreshTranscript();
            var prompt = new PendingPrompt(submitted, expanded, images);
            if (string.IsNullOrWhiteSpace(Draft) && !HasPendingImages && !HasPendingFiles && !HasPendingGitHub) RestorePrompt(prompt);
            else RecoveredPrompts.Add(prompt);
            throw;
        }
    }

    private void TryDispatchQueued()
    {
        if (queued is null || queueHeld || !queueReady || running || busy || stopping || disposed || !IsReady || dispatchingQueue) return;
        dispatchingQueue = true;
        queueReady = false;
        _ = DispatchQueuedAsync(queued);
    }

    private async Task DispatchQueuedAsync(PendingPrompt item)
    {
        ShowSubmittedPreview(item.Message, item.Images);
        try
        {
            await ExecuteAsync(async () => { await ShareMessageArtifactsAsync(item.Message); await session.SendAsync(item.Message, item.Images); });
            if (ReferenceEquals(queued, item)) queued = null;
        }
        catch (Exception exception) { submittedPreview = null; RefreshTranscript(); queueHeld = true; ReportError(exception); }
        finally { dispatchingQueue = false; NotifyQueue(); }
    }

    private void NotifyQueue()
    {
        foreach (var property in new[] { nameof(HasQueuedPrompt), nameof(QueuedPreview), nameof(QueueLabel), nameof(CanChangeQueue),
            nameof(CanReleaseQueue), nameof(CanSteerQueue), nameof(ComposerShowsStop), nameof(ShowSeparateStop),
            nameof(ComposerActionCommand), nameof(ComposerActionLabel), nameof(CanUseComposerAction) }) OnPropertyChanged(property);
    }
}
