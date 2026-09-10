using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

internal sealed class FakeConversationSession : IConversationSession
{
    public Func<ConversationOperation, string?, Task<System.Text.Json.JsonElement>>? OperationHandler { get; set; }
    public Task<System.Text.Json.JsonElement> RunOperationAsync(ConversationOperation operation, string? argument = null, CancellationToken cancellationToken = default) => OperationHandler?.Invoke(operation, argument) ?? Task.FromResult(default(System.Text.Json.JsonElement));
    public Task SetSessionNameAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public event Action<ConversationUpdate>? Updated;
    public List<string> Sent { get; } = [];
    public Exception? SendError { get; set; }
    public Exception? ModelError { get; set; }
    public Task SetThinkingLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        Emit(new() { HasThinkingLevelUpdate = true, ThinkingLevel = level });
        return Task.CompletedTask;
    }
    public Task SetApprovalModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        Emit(new() { HasApprovalModeUpdate = true, ApprovalMode = mode });
        return Task.CompletedTask;
    }
    public Task SetModelAsync(string provider, string modelId, CancellationToken cancellationToken = default)
    {
        if (ModelError is not null) return Task.FromException(ModelError);
        Emit(new() { HasModelUpdate = true, Model = new(provider, modelId, modelId) });
        return Task.CompletedTask;
    }
    public Exception? ConnectError { get; set; }
    public Task? ConnectDelay { get; set; }
    public int ConnectCount { get; private set; }
    public TaskCompletionSource ConnectStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Disposed { get; private set; }
    public void Emit(ConversationUpdate update) => Updated?.Invoke(update);
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ConnectCount++;
        Emit(new() { IsConnected = false, Status = "Connecting…", Error = "" });
        ConnectStarted.TrySetResult();
        if (ConnectDelay is not null) await ConnectDelay.WaitAsync(cancellationToken);
        if (ConnectError is not null) throw ConnectError;
        Emit(new() { IsConnected = true, Status = "Ready" });
    }
    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (SendError is not null) return Task.FromException(SendError);
        Sent.Add(message);
        Emit(new() { IsRunning = true, IsConnected = true, Status = "Working" });
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default) { Emit(new() { IsRunning = false, Status = "Stopped" }); return Task.CompletedTask; }
    public Task DisconnectAsync() { Emit(new() { IsConnected = false, IsRunning = false }); return Task.CompletedTask; }
    public Task ReplyAsync(string requestId, JsonObject response, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}
