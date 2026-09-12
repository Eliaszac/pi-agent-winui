using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

internal sealed class FakePiTransport : IPiTransport
{
    private readonly Channel<string> incoming = Channel.CreateUnbounded<string>();
    private readonly Channel<JsonElement> outgoing = Channel.CreateUnbounded<JsonElement>();
    public PiLaunchRequest? Launch { get; private set; }
    public bool Disposed { get; private set; }
    public bool AutoReply { get; set; }
    public bool EmitManualCompaction { get; set; }
    public bool RejectCompaction { get; set; }
    public string History { get; set; } = "[]";
    public bool IsStreaming { get; set; }
    public bool StartTurnOnPrompt { get; set; } = true;
    public bool ApplyModeCommands { get; set; }
    public string? TargetReadyId { get; set; }
    public string ModelId { get; private set; } = "fake";
    public string Provider { get; private set; } = "test";
    public string ExtensionCommands { get; set; } = "[]";
    public string SessionEntries { get; set; } = "[]";
    public bool RejectModel { get; set; }
    public bool RejectEntries { get; set; }
    public bool RejectThinking { get; set; }
    public string ThinkingLevel { get; set; } = "medium";
    public string[] ThinkingLevels { get; set; } = ["off", "low", "medium", "high"];
    public ConcurrentQueue<string> Commands { get; } = new();
    public Task StartAsync(PiLaunchRequest request, CancellationToken cancellationToken = default)
    {
        Launch = request;
        return Task.CompletedTask;
    }
    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var line in incoming.Reader.ReadAllAsync(cancellationToken)) yield return line;
    }
    public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        var packet = JsonDocument.Parse(line).RootElement.Clone();
        var command = packet.GetProperty("type").GetString()!;
        Commands.Enqueue(command);
        outgoing.Writer.TryWrite(packet);
        if (AutoReply && command != "extension_ui_response")
        {
            if (command == "compact" && EmitManualCompaction)
            {
                Push("""{"type":"compaction_start"}""");
                Push("""{"type":"compaction_end"}""");
            }
            if (command == "compact" && RejectCompaction) { Reply(packet, success: false); return Task.CompletedTask; }
            if (command == "set_thinking_level" && RejectThinking) { Reply(packet, success: false); return Task.CompletedTask; }
            if (command == "set_thinking_level") ThinkingLevel = packet.GetProperty("level").GetString()!;
            if ((command == "set_model" && RejectModel) || (command == "get_entries" && RejectEntries)) { Reply(packet, success: false); return Task.CompletedTask; }
            if (command == "set_model") { ModelId = packet.GetProperty("modelId").GetString()!; Provider = packet.GetProperty("provider").GetString()!; }
            if (command == "prompt") IsStreaming = StartTurnOnPrompt;
            if (command == "prompt" && packet.GetProperty("message").GetString() == "/pi-gui-target-check" && TargetReadyId is not null)
                Push(JsonSerializer.Serialize(new { type = "extension_ui_request", method = "setStatus", statusKey = "pi-gui-target-ready", statusText = TargetReadyId }));
            if (command == "prompt" && ApplyModeCommands && packet.GetProperty("message").GetString() is { } message && message.StartsWith("/mode ", StringComparison.Ordinal))
                SessionEntries = JsonSerializer.Serialize(new[] { new { type = "custom", customType = "modes", data = new { currentMode = message[6..] } } });
            if (command == "abort") IsStreaming = false;
            object? data = command switch
            {
                "get_state" => new { sessionFile = Launch!.SessionFile, isStreaming = IsStreaming, isCompacting = false, thinkingLevel = ThinkingLevel, model = new { provider = Provider, id = ModelId } },
                "get_available_thinking_levels" => new { levels = ThinkingLevels },
                "get_commands" => new { commands = JsonDocument.Parse(ExtensionCommands).RootElement.Clone() },
                "get_entries" => new { entries = JsonDocument.Parse(SessionEntries).RootElement.Clone() },
                "get_messages" => new { messages = JsonDocument.Parse(History).RootElement.Clone() },
                "get_available_models" => new { models = new[] { new { provider = "test", id = "fake" }, new { provider = "test", id = "other" } } },
                "set_model" => new { provider = packet.GetProperty("provider").GetString(), id = packet.GetProperty("modelId").GetString() },
                _ => null
            };
            Reply(packet, data);
        }
        return Task.CompletedTask;
    }
    public Task<JsonElement> NextRequestAsync() => outgoing.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
    public void Push(string line) => incoming.Writer.TryWrite(line);
    public void Reply(JsonElement request, object? data = null, bool success = true) =>
        Push(JsonSerializer.Serialize(new { type = "response", id = request.GetProperty("id").GetString(),
            command = request.GetProperty("type").GetString(), success, data, error = success ? null : "Rejected" }));
    public void Exit() => incoming.Writer.TryComplete();
    public ValueTask DisposeAsync() { Disposed = true; incoming.Writer.TryComplete(); return ValueTask.CompletedTask; }
}
