using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    public ArtifactPanelViewModel? Artifacts { get; internal set; }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> artifactOperations = new();
    private readonly List<Models.Conversations.ChatEntry> artifactImages = [];
    private bool refreshArtifacts;

    private async void TrackArtifactOperation(Task operation)
    {
        artifactOperations.TryAdd(operation, 0);
        try { await operation; }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!disposed) ReportAttachmentError("Artifact operation failed: " + error.Message); }
        finally { artifactOperations.TryRemove(operation, out _); }
    }

    private async Task HandleArtifactRequestAsync(JsonElement packet)
    {
        JsonObject result;
        try
        {
            using var request = JsonDocument.Parse(PiJson.Text(packet, "placeholder"));
            result = Artifacts is { } artifacts ? await artifacts.HandleAsync(request.RootElement.Clone())
                : throw new IOException("Artifacts are unavailable for this conversation.");
        }
        catch (Exception error) { result = new() { ["error"] = error.Message }; }
        if (!disposed) await session.ReplyAsync(PiJson.Text(packet, "id"), new() { ["value"] = result.ToJsonString() });
    }
}
