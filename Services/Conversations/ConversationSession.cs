using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Coordinates one Pi process and its persistent session, never a shared selected-session process.</summary>
public sealed class ConversationSession(PiLaunchRequest launch, Func<PiRpcClient> clientFactory, Func<bool>? permissionConfigured = null) : IConversationSession
{
    public Func<JsonElement, Task>? ResearchRequested { get; init; }
    private async Task HandleResearchAsync(PiRpcClient current, JsonElement packet)
    {
        JsonObject response;
        try
        {
            using var payload = JsonDocument.Parse(PiJson.Text(packet, "placeholder"));
            if (ResearchRequested is null) throw new InvalidOperationException("Background research is unavailable.");
            await ResearchRequested(payload.RootElement.Clone());
            response = new() { ["value"] = "{\"accepted\":true}" };
        }
        catch (Exception exception) { response = new() { ["value"] = new JsonObject { ["error"] = exception.Message }.ToJsonString() }; }
        try { await current.ReplyToExtensionAsync(PiJson.Text(packet, "id"), response, lifetime.Token); }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException) { }
    }
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private readonly object stateGate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly PiTranscript transcript = new();
    private readonly RunUsageTracker runUsage = new();
    private PiRpcClient? client;
    public Models.Pi.ProcessIdentity? ProcessIdentity => client?.ProcessIdentity;
    private bool connected;
    private bool running;
    private bool changingModel;
    private bool awaitingSettled;
    private bool disposed;
    private PermissionModesIntegration? permissions;
    private ThinkingIntegration? thinking;
    private bool thinkingInitialized;
    private string? manualName = launch.SessionName;
    public event Action<ConversationUpdate>? Updated;

    public async Task CopySessionAsync(string destination, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        destination = Path.GetFullPath(destination);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(destination), Path.GetDirectoryName(Path.GetFullPath(launch.SessionFile))) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(destination), "N", out _) || Path.GetExtension(destination) != ".jsonl" ||
            StringComparer.OrdinalIgnoreCase.Equals(destination, Path.GetFullPath(launch.SessionFile)))
            throw new ArgumentException("The copy must have a new identity in the same project.", nameof(destination));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("Wait for this conversation to finish before copying it.");
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            changingModel = true;
        }
        try
        {
            if (File.Exists(destination)) throw new IOException("The destination conversation already exists.");
            var packet = await current.RequestAsync("get_commands", cancellationToken: linked.Token).ConfigureAwait(false);
            var commands = PiJson.Field(PiJson.Field(packet, "data"), "commands");
            if (commands.ValueKind != JsonValueKind.Array || !commands.EnumerateArray().Any(item => PiJson.Text(item, "name") == "pi-gui-copy-session"))
                throw new InvalidOperationException("Restart Pi Agent to load the conversation-copy integration.");
            var request = new JsonObject { ["target"] = destination, ["title"] = title.Trim() };
            await current.RequestAsync("prompt", new JsonObject { ["message"] = "/pi-gui-copy-session " + request.ToJsonString() }, linked.Token).ConfigureAwait(false);
            if (!File.Exists(destination)) throw new IOException("Pi couldn't create the conversation copy. The original is unchanged.");
        }
        finally { lock (stateGate) changingModel = false; }
    }

    public async Task<JsonElement> RunOperationAsync(ConversationOperation operation, string? argument = null, CancellationToken cancellationToken = default)
    {
        // Inventory reads may run alongside an agent turn and must not reserve mutation state.
        if (operation == ConversationOperation.Commands)
        {
            PiRpcClient inventoryClient;
            lock (stateGate) inventoryClient = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            using var inventoryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            var response = await inventoryClient.RequestAsync("get_commands", cancellationToken: inventoryCancellation.Token).ConfigureAwait(false);
            return PiJson.Field(response, "data");
        }
        // Keep the transport private and allow only operations that preserve this workspace's session identity.
        var command = operation switch
        {
            ConversationOperation.Commands => "get_commands",
            ConversationOperation.Details => "get_session_stats",
            ConversationOperation.State => "get_state",
            ConversationOperation.RefreshModels => "prompt",
            ConversationOperation.Instructions => "prompt",
            ConversationOperation.Compact => "compact",
            ConversationOperation.ExportHtml => "export_html",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("Wait for this conversation to finish before using commands.");
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            changingModel = true;
        }
        try
        {
            if (operation is ConversationOperation.RefreshModels or ConversationOperation.Instructions)
            {
                var expected = operation == ConversationOperation.Instructions ? "pi-gui-instructions" : "pi-gui-refresh-models";
                var discovery = await current.RequestAsync("get_commands", cancellationToken: linked.Token).ConfigureAwait(false);
                var commands = PiJson.Field(PiJson.Field(discovery, "data"), "commands");
                if (commands.ValueKind != JsonValueKind.Array || !commands.EnumerateArray().Any(item => PiJson.Text(item, "name") == expected))
                    throw new InvalidOperationException("Restart Pi Agent to load this integration in the conversation.");
            }
            JsonObject? arguments = operation switch
            {
                ConversationOperation.RefreshModels => new() { ["message"] = "/pi-gui-refresh-models" },
                ConversationOperation.Instructions => new() { ["message"] = "/pi-gui-instructions" },
                ConversationOperation.Compact when !string.IsNullOrWhiteSpace(argument) => new() { ["customInstructions"] = argument },
                ConversationOperation.ExportHtml => new() { ["outputPath"] = Path.GetFullPath(argument ?? throw new ArgumentException("Choose an export file.")) },
                _ => null
            };
            var result = await current.RequestAsync(command, arguments, linked.Token,
                timeout: operation == ConversationOperation.Compact ? TimeSpan.FromMinutes(10) : null).ConfigureAwait(false);
            if (operation == ConversationOperation.RefreshModels)
            {
                await current.RequestAsync("get_available_models", cancellationToken: linked.Token, applyResponse: packet =>
                    Publish(new() { AvailableModels = PiModelParser.ParseList(PiJson.Field(PiJson.Field(packet, "data"), "models")) })).ConfigureAwait(false);
                await current.RequestAsync("get_state", cancellationToken: linked.Token, applyResponse: ApplyState).ConfigureAwait(false);
            }
            if (operation == ConversationOperation.Compact)
            {
                // Manual compaction can finish without an agent_settled event.
                lock (stateGate) awaitingSettled = false;
                await current.RequestAsync("get_state", cancellationToken: linked.Token, applyResponse: ApplyState).ConfigureAwait(false);
            }
            return PiJson.Field(result, "data");
        }
        catch (PiCommandException)
        {
            if (operation == ConversationOperation.Compact)
            {
                lock (stateGate) awaitingSettled = false;
                await current.RequestAsync("get_state", cancellationToken: linked.Token, applyResponse: ApplyState).ConfigureAwait(false);
            }
            throw;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            await DisconnectAsync().ConfigureAwait(false);
            OnFault(exception);
            throw;
        }
        finally { lock (stateGate) changingModel = false; }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        cancellationToken = linked.Token;
        await connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (connected) return;
            await ReleaseClientAsync().ConfigureAwait(false);
            var isNewSession = !thinkingInitialized && !File.Exists(launch.SessionFile);
            Publish(new() { Status = "Connecting…", IsConnected = false, Error = "" });
            var next = clientFactory();
            client = next;
            next.EventReceived += OnEvent;
            next.Faulted += OnFault;
            await next.StartAsync(launch with { SessionName = manualName }, cancellationToken).ConfigureAwait(false);
            thinking = new(Publish);
            await next.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
            await next.RequestAsync("get_available_models", cancellationToken: cancellationToken, applyResponse: packet =>
                Publish(new() { AvailableModels = PiModelParser.ParseList(PiJson.Field(PiJson.Field(packet, "data"), "models")) })).ConfigureAwait(false);
            permissions = new(Publish, permissionConfigured);
            await permissions.DiscoverAsync(next, cancellationToken).ConfigureAwait(false);
            if (permissions.DefaultApplied)
                await next.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
            await thinking.RefreshAsync(next, cancellationToken).ConfigureAwait(false);
            if (isNewSession && thinking.Levels.Contains("low"))
            {
                await thinking.SetAsync(next, "low", cancellationToken).ConfigureAwait(false);
                await next.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
            }
            thinkingInitialized = true;
            await next.RequestAsync("get_messages", cancellationToken: cancellationToken, applyResponse: packet =>
            {
                lock (stateGate)
                {
                    var history = transcript.Load(PiJson.Field(PiJson.Field(packet, "data"), "messages"));
                    connected = true;
                    Publish(new() { History = history, Status = running ? "Working" : "Ready", IsRunning = running, IsConnected = true, Error = "" });
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            connected = false;
            await ReleaseClientAsync().ConfigureAwait(false);
            throw;
        }
        finally { connectionGate.Release(); }
    }

    public Task SendAsync(string message, CancellationToken cancellationToken = default) => SendAsync(message, [], cancellationToken);

    public async Task SendAsync(string message, IReadOnlyList<ChatImage> images, CancellationToken cancellationToken = default)
    {
        if (images.Count == 0) ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var payload = new JsonObject { ["message"] = message };
        if (images.Count > 0) payload["images"] = PiImageContent.Serialize(images);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("This conversation is busy. Wait before sending again.");
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            running = true;
            Publish(new() { Status = "Sending…", IsRunning = true, Error = "" });
        }
        try { await current.RequestAsync("prompt", payload, cancellationToken).ConfigureAwait(false); }
        catch (PiCommandException)
        {
            lock (stateGate) { running = false; Publish(new() { Status = "Ready", IsRunning = false }); }
            throw;
        }
        catch (Exception exception)
        {
            await DisconnectAsync().ConfigureAwait(false);
            OnFault(exception);
            throw;
        }
        // Extension commands can be handled without starting an agent turn. Reconcile that accepted state.
        // A later connection failure must not turn an acknowledged prompt into an unsent draft.
        try
        {
            await current.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
            await thinking!.RefreshAsync(current, cancellationToken).ConfigureAwait(false);
            if (permissions?.Available == true) await permissions.RefreshAsync(current, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) { await DisconnectAsync().ConfigureAwait(false); OnFault(exception); }
    }

    private readonly List<PendingPrompt> pendingSteering = [];
    public async Task SteerAsync(string message, IReadOnlyList<ChatImage> images, CancellationToken cancellationToken = default)
    {
        if (images.Count == 0) ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (message.TrimStart().StartsWith('/')) throw new InvalidOperationException("Steering accepts a message, not a slash command.");
        var payload = new JsonObject { ["message"] = message, ["streamingBehavior"] = "steer" };
        if (images.Count > 0) payload["images"] = PiImageContent.Serialize(images);
        PiRpcClient current;
        var pending = new PendingPrompt(message, message, images.ToArray());
        lock (stateGate)
        {
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            pendingSteering.Add(pending);
        }
        try { await current.RequestAsync("prompt", payload, cancellationToken).ConfigureAwait(false); }
        catch (PiCommandException) { lock (stateGate) pendingSteering.Remove(pending); throw; }
        catch (Exception exception) { await DisconnectAsync().ConfigureAwait(false); OnFault(exception); throw; }
    }

    private void ApplyState(JsonElement packet)
    {
        lock (stateGate)
        {
            var state = PiJson.Field(packet, "data");
            var file = PiJson.Text(state, "sessionFile");
            if (file.Length == 0 || !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(file), Path.GetFullPath(launch.SessionFile)))
                throw new InvalidDataException("Pi did not open this conversation's session file.");
            running = awaitingSettled || PiJson.Flag(state, "isStreaming") || PiJson.Flag(state, "isCompacting");
            var model = PiJson.Field(state, "model");
            if (PiJson.Text(state, "sessionName") is { Length: > 0 } sessionName) Publish(new() { SessionName = sessionName });
            Publish(new() { Status = running ? "Working" : "Ready", IsRunning = running,
                HasThinkingLevelUpdate = true, ThinkingLevel = PiJson.Text(state, "thinkingLevel"),
                HasModelUpdate = true, Model = model.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : PiModelParser.Parse(model) });
        }
    }

    public async Task SetModelAsync(string provider, string modelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("Wait for this conversation to finish before changing models.");
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            changingModel = true;
        }
        try
        {
            await current.RequestAsync("set_model", new JsonObject { ["provider"] = provider, ["modelId"] = modelId },
                cancellationToken, packet => Publish(new() { HasModelUpdate = true, Model = PiModelParser.Parse(PiJson.Field(packet, "data")) })).ConfigureAwait(false);
            await current.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
            await thinking!.RefreshAsync(current, cancellationToken).ConfigureAwait(false);
        }
        catch (PiCommandException) { throw; }
        catch (Exception exception) { await DisconnectAsync().ConfigureAwait(false); OnFault(exception); throw; }
        finally { lock (stateGate) changingModel = false; }
    }

    public async Task SetThinkingLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("Wait for this conversation to finish before changing effort.");
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            changingModel = true;
        }
        try
        {
            await thinking!.SetAsync(current, level, cancellationToken).ConfigureAwait(false);
            await current.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
        }
        catch (PiCommandException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception exception) { await DisconnectAsync().ConfigureAwait(false); OnFault(exception); throw; }
        finally { lock (stateGate) changingModel = false; }
    }

    public async Task SetApprovalModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        PiRpcClient current;
        lock (stateGate)
        {
            if (running || changingModel) throw new InvalidOperationException("Wait for this conversation to finish before changing modes.");
            current = connected && client is not null ? client : throw new IOException("Pi is disconnected.");
            changingModel = true;
        }
        try
        {
            var integration = permissions ?? throw new InvalidOperationException("The approval extension is unavailable.");
            await integration.SetAsync(current, mode, cancellationToken).ConfigureAwait(false);
            // Extension model profiles may change the model too. Reflect Pi's actual conversation state.
            await current.RequestAsync("get_state", cancellationToken: cancellationToken, applyResponse: ApplyState).ConfigureAwait(false);
            await thinking!.RefreshAsync(current, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Publish(new() { HasApprovalModeUpdate = true });
            throw;
        }
        finally { lock (stateGate) changingModel = false; }
    }

    private async Task RefreshExtensionStateAsync()
    {
        await connectionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || !connected || client is null) return;
            if (permissions?.Available == true) await permissions.RefreshAsync(client, lifetime.Token).ConfigureAwait(false);
            await client.RequestAsync("get_state", cancellationToken: lifetime.Token, applyResponse: ApplyState).ConfigureAwait(false);
            await thinking!.RefreshAsync(client, lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (!disposed) Publish(new() { Error = $"Couldn't refresh conversation settings: {exception.Message}" });
        }
        finally { connectionGate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var current = client ?? throw new IOException("Pi is disconnected.");
        Publish(new() { Status = "Stopping…" });
        PendingPrompt[] beforeStop;
        lock (stateGate) beforeStop = pendingSteering.ToArray();
        try
        {
            await current.RequestAsync("clear_queue", cancellationToken: cancellationToken, applyResponse: packet =>
            {
                var data = PiJson.Field(packet, "data");
                var recovered = new List<PendingPrompt>();
                var saved = beforeStop.ToList();
                foreach (var name in new[] { "steering", "followUp" })
                {
                    var values = PiJson.Field(data, name);
                    if (values.ValueKind != JsonValueKind.Array) continue;
                    foreach (var value in values.EnumerateArray())
                    {
                        if (value.ValueKind != JsonValueKind.String) continue;
                        var text = value.GetString()!;
                        var match = saved.FirstOrDefault(item => item.Message == text);
                        if (match is not null) saved.Remove(match);
                        recovered.Add(match ?? new(text, text, []));
                    }
                }
                lock (stateGate) pendingSteering.Clear();
                Publish(new() { RecoveredPrompts = recovered, SteeringQueue = [] });
            }).ConfigureAwait(false);
            await current.RequestAsync("abort", cancellationToken: cancellationToken).ConfigureAwait(false);
            lock (stateGate) { awaitingSettled = false; running = false; Publish(new() { Status = "Stopped", IsRunning = false, DismissPrompts = true }); }
        }
        catch (Exception exception) { await DisconnectAsync().ConfigureAwait(false); OnFault(exception); throw; }
    }

    public async Task SetSessionNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            manualName = name.Trim();
            if (connected && client is not null)
                await client.RequestAsync("set_session_name", new JsonObject { ["name"] = manualName }, cancellationToken).ConfigureAwait(false);
        }
        finally { connectionGate.Release(); }
    }

    public Task ReplyAsync(string requestId, JsonObject response, CancellationToken cancellationToken = default) =>
        (client ?? throw new IOException("Pi is disconnected.")).ReplyToExtensionAsync(requestId, response, cancellationToken);

    private void OnEvent(JsonElement packet)
    {
        lock (stateGate)
        {
            runUsage.Observe(packet);
            var entry = transcript.Apply(packet);
            if (entry is not null) Publish(new() { Entry = entry });
            switch (PiJson.Text(packet, "type"))
            {
                case "queue_update":
                    var steering = PiJson.Field(packet, "steering");
                    if (steering.ValueKind == JsonValueKind.Array)
                    {
                        var texts = steering.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray();
                        pendingSteering.RemoveAll(item => !texts.Contains(item.Message));
                        Publish(new() { SteeringQueue = texts });
                    }
                    break;
                case "session_info_changed":
                    if (PiJson.Text(packet, "name") is { Length: > 0 } name) Publish(new() { SessionName = name });
                    break;
                case "agent_start": awaitingSettled = true; running = true; Publish(new() { Status = "Working", IsRunning = true }); break;
                case "agent_end": Publish(new() { Status = "Finishing…" }); break;
                case "agent_settled":
                    awaitingSettled = false; running = false;
                    Publish(new() { Status = "Ready", IsRunning = false, IsCompacting = false, DismissPrompts = true, TurnCompleted = true, RunUsage = runUsage.Complete() });
                    _ = Task.Run(RefreshExtensionStateAsync);
                    break;
                case "compaction_start": awaitingSettled = true; running = true; Publish(new() { Status = "Compacting context…", IsRunning = true, IsCompacting = true }); break;
                case "auto_retry_start": awaitingSettled = true; running = true; Publish(new() { Status = "Retrying provider request…", IsRunning = true }); break;
                case "extension_error": Publish(new() { Error = PiJson.Text(packet, "error") }); break;
                case "compaction_end":
                    Publish(new() { IsCompacting = false });
                    if (PiJson.Text(packet, "errorMessage") is { Length: > 0 } message) Publish(new() { Error = message });
                    break;
                case "extension_ui_request": HandleExtension(packet); break;
            }
        }
    }

    private void HandleExtension(JsonElement packet)
    {
        if (PiJson.Text(packet, "method") == "setStatus" && PiJson.Text(packet, "statusKey") == InstructionSnapshotParser.StatusKey)
        {
            if (InstructionSnapshotParser.Parse(PiJson.Text(packet, "statusText")) is { } snapshot)
                Publish(new() { Instructions = snapshot });
            return;
        }
        if (PiJson.Text(packet, "method") == "setStatus" && PiJson.Text(packet, "statusKey") == CapabilityParser.McpStatusKey)
        {
            if (CapabilityParser.Mcp(PiJson.Text(packet, "statusText")) is { } snapshot)
                Publish(new() { McpStatus = snapshot });
            return;
        }
        if (PiJson.Text(packet, "method") == "input" && PiJson.Text(packet, "title") == "pi-gui-background-research-v1" && client is { } current)
        {
            _ = Task.Run(() => HandleResearchAsync(current, packet.Clone()));
            return;
        }
        var method = PiJson.Text(packet, "method");
        if (method is "confirm" or "select" or "input" or "editor")
        {
            var options = PiJson.Field(packet, "options");
            var values = options.ValueKind == JsonValueKind.Array ? options.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!).ToArray() : [];
            Publish(new() { Prompt = new ExtensionPrompt(PiJson.Text(packet, "id"), method, PiJson.Text(packet, "title"),
                PiJson.Text(packet, "message"), values, PiJson.Text(packet, "prefill"), PiJson.Number(packet, "timeout") is > 0 and var timeout ? timeout : null) });
        }
        else if (method == "notify")
        {
            var message = PiJson.Text(packet, "message");
            switch (PiJson.Text(packet, "notifyType"))
            {
                case "error": Publish(new() { Error = message }); break;
                case "warning": Publish(new() { Warning = message }); break;
                // Informational notices do not mark a successful run as failed.
            }
        }
    }

    private void OnFault(Exception exception)
    {
        lock (stateGate)
        {
            connected = false;
            running = false;
            awaitingSettled = false;
            runUsage.Reset();
            Publish(new() { Status = "Disconnected", IsConnected = false, IsRunning = false,
                ErrorDiagnostics = Utilities.ErrorDiagnostics.Create(exception),
                Error = exception is JsonException ? "Pi sent invalid JSON. Reconnect to reopen the session." : exception.Message });
        }
    }

    private void Publish(ConversationUpdate update) => Updated?.Invoke(update);

    private async Task ReleaseClientAsync()
    {
        lock (stateGate) pendingSteering.Clear();
        Publish(new() { SteeringQueue = [] });
        if (client is null) return;
        var previous = client;
        client = null;
        previous.EventReceived -= OnEvent;
        previous.Faulted -= OnFault;
        await previous.DisposeAsync().ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        await connectionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await ReleaseClientAsync().ConfigureAwait(false);
            lock (stateGate) { connected = false; running = false; awaitingSettled = false; runUsage.Reset(); Publish(new() { Status = "Disconnected", IsConnected = false, IsRunning = false }); }
        }
        finally { connectionGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        await DisconnectAsync().ConfigureAwait(false);
    }
}
