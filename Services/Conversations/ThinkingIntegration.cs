using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Reads model capabilities and changes Pi-owned session reasoning settings.</summary>
public sealed class ThinkingIntegration(Action<ConversationUpdate> publish)
{
    public IReadOnlyList<string> Levels { get; private set; } = [];

    public async Task RefreshAsync(PiRpcClient client, CancellationToken cancellationToken)
    {
        await client.RequestAsync("get_available_thinking_levels", cancellationToken: cancellationToken, applyResponse: packet =>
        {
            var levels = PiJson.Field(PiJson.Field(packet, "data"), "levels");
            if (levels.ValueKind != JsonValueKind.Array || levels.EnumerateArray().Any(level => level.ValueKind != JsonValueKind.String))
                throw new InvalidDataException("Pi returned invalid effort levels.");
            Levels = levels.EnumerateArray().Select(level => level.GetString()!).Distinct().ToArray();
            publish(new() { ThinkingLevels = Levels });
        }).ConfigureAwait(false);
    }

    public async Task SetAsync(PiRpcClient client, string level, CancellationToken cancellationToken)
    {
        if (!Levels.Contains(level)) throw new InvalidOperationException("This model does not support that effort level.");
        await client.RequestAsync("set_thinking_level", new JsonObject { ["level"] = level }, cancellationToken).ConfigureAwait(false);
    }
}
