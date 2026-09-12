using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>The agent runtime boundary for one independent conversation.</summary>
public interface IConversationSession : IAsyncDisposable
{
    Models.Pi.ProcessIdentity? ProcessIdentity => null;
    event Action<ConversationUpdate>? Updated;
    Task<System.Text.Json.JsonElement> CheckpointAsync(JsonObject request, CancellationToken cancellationToken = default) =>
        Task.FromException<System.Text.Json.JsonElement>(new NotSupportedException("Workspace checkpoints are unavailable."));
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string message, CancellationToken cancellationToken = default);
    Task SendAsync(string message, IReadOnlyList<ChatImage> images, CancellationToken cancellationToken = default);
    Task SteerAsync(string message, IReadOnlyList<ChatImage> images, CancellationToken cancellationToken = default);
    Task SetModelAsync(string provider, string modelId, CancellationToken cancellationToken = default);
    Task SetApprovalModeAsync(string mode, CancellationToken cancellationToken = default);
    Task SetThinkingLevelAsync(string level, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SetSessionNameAsync(string name, CancellationToken cancellationToken = default);
    Task CopySessionAsync(string destination, string title, CancellationToken cancellationToken = default);
    Task<System.Text.Json.JsonElement> RunOperationAsync(ConversationOperation operation, string? argument = null, CancellationToken cancellationToken = default);
    Task ReplyAsync(string requestId, JsonObject response, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
