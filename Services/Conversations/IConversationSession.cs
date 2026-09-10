using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>The agent runtime boundary for one independent conversation.</summary>
public interface IConversationSession : IAsyncDisposable
{
    event Action<ConversationUpdate>? Updated;
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string message, CancellationToken cancellationToken = default);
    Task SetModelAsync(string provider, string modelId, CancellationToken cancellationToken = default);
    Task SetApprovalModeAsync(string mode, CancellationToken cancellationToken = default);
    Task SetThinkingLevelAsync(string level, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SetSessionNameAsync(string name, CancellationToken cancellationToken = default);
    Task<System.Text.Json.JsonElement> RunOperationAsync(ConversationOperation operation, string? argument = null, CancellationToken cancellationToken = default);
    Task ReplyAsync(string requestId, JsonObject response, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
