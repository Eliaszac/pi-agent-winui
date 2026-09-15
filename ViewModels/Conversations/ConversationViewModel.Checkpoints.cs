using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    private readonly Dictionary<string, CheckpointManifest> checkpoints = [];
    public string CheckpointStatus { get; private set; } = "Enable Workspace checkpoints in Extensions to capture future changes.";
    public string? CheckpointRecoveryId { get; private set; }
    public bool HasCheckpointRecovery => CheckpointRecoveryId is not null;
    public bool HasCheckpointError { get; private set; }
    private bool checkpointsInitializing;

    private void ObserveCheckpoint(JsonElement packet)
    {
        var state = PiJson.Text(packet, "status");
        if (state.Length > 0)
        {
            checkpointsInitializing = state == "initializing";
            HasCheckpointError = state == "error";
            CheckpointStatus = state switch
            {
                "ready" => "Workspace checkpoints ready",
                "initializing" => "Preparing checkpoints…",
                "capturing" => "Capturing workspace changes…",
                "disabled" => "Enable Workspace checkpoints in Extensions to capture future changes.",
                "recovery" => "An interrupted checkpoint needs inspection. Stop any remote commands before inspecting recovery.",
                _ => PiJson.Text(packet, "error")
            };
            if (state == "error") warning = CheckpointStatus;
            if (state == "recovery") CheckpointRecoveryId = PiJson.Text(packet, "recoveryId");
            OnPropertyChanged(nameof(CheckpointStatus));
            OnPropertyChanged(nameof(HasCheckpointError));
            OnPropertyChanged(nameof(CheckpointRecoveryId));
            OnPropertyChanged(nameof(HasCheckpointRecovery));
        }
        if (CheckpointManifestParser.Parse(PiJson.Field(packet, "manifest")) is { } manifest)
            checkpoints[manifest.Response] = manifest;
    }

    private void ApplyCheckpointSummaries()
    {
        foreach (var (response, manifest) in checkpoints)
        {
            if (!entries.TryGetValue(response, out var entry)) continue;
            if (entry.Summary?.CheckpointId == manifest.Id && ReferenceEquals(entry.Summary, checkpointSummaries.GetValueOrDefault(response))) continue;
            var current = entry.Summary;
            var summary = new RunChangesViewModel(manifest.Files, manifest.Applied.Count == 0 ? current?.VerificationLabels : [],
                manifest.Applied.Count == 0 ? current?.DiagnosticsLabel : null, IsRemoteTarget)
            {
                CheckpointId = checkpointsInitializing || manifest.State == "expired" ? null : manifest.Id,
                CanUndoRevert = manifest.Applied.Count > 0,
                CaptureNotice = manifest.State == "expired" ? "Checkpoint expired. File restoration is unavailable." : manifest.Overlap ? "Overlapping work detected. Automatic revert is unavailable." : manifest.Omitted > 0
                    ? $"{manifest.Omitted} paths could not be captured. Automatic revert is unavailable." : manifest.Applied.Count > 0
                    ? $"Reverted {manifest.Applied.Count} files. Earlier verification no longer describes the workspace." : ""
            };
            entry.Summary = summary;
            checkpointSummaries[response] = summary;
            if (ReferenceEquals(entry, responseActionsEntry)) RunChanges = summary;
        }
    }
    private readonly Dictionary<string, RunChangesViewModel> checkpointSummaries = [];

    public async Task<JsonElement> CheckpointOperationAsync(string action, RunChangesViewModel? summary = null,
        bool undo = false, IReadOnlyList<string>? paths = null)
    {
        if (!IsReady || running || busy || operationInFlight) throw new InvalidOperationException("Finish current work before using checkpoints.");
        if (checkpointsInitializing) throw new InvalidOperationException("Checkpoints are still preparing. Try again shortly.");
        var id = action == "recover" ? CheckpointRecoveryId : summary?.CheckpointId;
        if (action != "refresh" && id is null)
            throw new InvalidOperationException("This request has no checkpoint. Enable Workspace checkpoints in Extensions before making future changes.");
        operationInFlight = busy = true;
        NotifyState();
        try
        {
            var request = new JsonObject { ["action"] = action, ["id"] = id, ["undo"] = undo };
            if (paths is not null) request["paths"] = new JsonArray(paths.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray());
            var result = await session.CheckpointAsync(request);
            if (action == "recover")
            {
                CheckpointRecoveryId = null;
                OnPropertyChanged(nameof(HasCheckpointRecovery));
            }
            return result;
        }
        finally { operationInFlight = busy = false; NotifyState(); }
    }
}
