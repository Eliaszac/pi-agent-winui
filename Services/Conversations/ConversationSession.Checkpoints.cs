using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

public sealed partial class ConversationSession
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> checkpointRequests = new();
    private void HandleCheckpointPacket(string text)
    {
        if (text.Length > 2_000_000) { Publish(new() { Warning = "Checkpoint response exceeded its limit." }); return; }
        try
        {
            using var document = JsonDocument.Parse(text);
            var packet = document.RootElement;
            if (PiJson.Number(packet, "version") != 1) return;
            var id = PiJson.Text(packet, "requestId");
            if (checkpointRequests.TryGetValue(id, out var completion))
            {
                if (PiJson.Text(packet, "error") is { Length: > 0 } error) completion.TrySetException(new IOException(error));
                else completion.TrySetResult(PiJson.Field(packet, "result").Clone());
            }
            Publish(new() { Checkpoint = packet.Clone() });
            if (PiJson.Text(packet, "status") is "ready" or "disabled" && WorkspaceActivityLease.PendingRecovery(launch.SessionFile) is { } recovery)
            {
                using var recoveryPacket = JsonDocument.Parse(JsonSerializer.Serialize(new { version = 1, status = "recovery", recoveryId = recovery }));
                Publish(new() { Checkpoint = recoveryPacket.RootElement.Clone() });
            }
        }
        catch (JsonException) { Publish(new() { Warning = "Invalid checkpoint response." }); }
    }
    public async Task<JsonElement> CheckpointAsync(JsonObject request, CancellationToken cancellationToken = default)
    {
        Pi.PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("Finish the current operation before using checkpoints.");
            current = connected && client is not null ? client : throw new IOException("Connect this conversation to use checkpoints.");
            changingModel = true;
        }
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        checkpointRequests[id] = completion;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        linked.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            using var restoreLease = request["action"]?.GetValue<string>() is "apply" or "recover" ? WorkspaceActivityLease.Acquire(true) : null;
            var discovery = await current.RequestAsync("get_commands", cancellationToken: linked.Token).ConfigureAwait(false);
            var commands = PiJson.Field(PiJson.Field(discovery, "data"), "commands");
            if (commands.ValueKind != JsonValueKind.Array || !commands.EnumerateArray().Any(item => PiJson.Text(item, "name") == "pi-gui-checkpoint"))
                throw new InvalidOperationException("Restart Pi desktop to load the checkpoint extension.");
            request = (JsonObject)request.DeepClone();
            request["requestId"] = id;
            var mutation = request["action"]?.GetValue<string>() is "apply" or "recover";
            if (mutation) WorkspaceActivityLease.MarkRecovery(launch.SessionFile, request["id"]!.GetValue<string>());
            await current.RequestAsync("prompt", new JsonObject { ["message"] = "/pi-gui-checkpoint " + request.ToJsonString() }, linked.Token).ConfigureAwait(false);
            var result = await completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            if (mutation) WorkspaceActivityLease.ClearRecovery(launch.SessionFile);
            return result;
        }
        catch (OperationCanceledException) { throw new IOException("Checkpoint operation interrupted. Its outcome may be uncertain; inspect recovery before retrying."); }
        finally
        {
            checkpointRequests.TryRemove(id, out _);
            lock (stateGate) changingModel = false;
            if (WorkspaceActivityLease.PendingRecovery(launch.SessionFile) is { } recovery)
            {
                using var packet = JsonDocument.Parse(JsonSerializer.Serialize(new { version = 1, status = "recovery", recoveryId = recovery }));
                Publish(new() { Checkpoint = packet.RootElement.Clone() });
            }
        }
    }
}
