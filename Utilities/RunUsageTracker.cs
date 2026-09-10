using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Tracks a live run across model/tool cycles without estimating tokens from response text.</summary>
public sealed class RunUsageTracker(TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly Dictionary<string, long> usage = [];
    private long? started;
    private bool missingAssistantUsage;
    private bool sawAssistant;

    public void Observe(JsonElement packet)
    {
        var type = PiJson.Text(packet, "type");
        if (type == "agent_start" && started is null)
        {
            Reset();
            started = clock.GetTimestamp();
        }
        if (started is null) return;
        if (type == "message_end")
        {
            var message = PiJson.Field(packet, "message");
            var role = PiJson.Text(message, "role");
            if (role is not ("assistant" or "toolResult")) return;
            if (role == "assistant") sawAssistant = true;
            var total = ReadTokens(PiJson.Field(message, "usage"));
            if (total is null)
            {
                if (role == "assistant") missingAssistantUsage = true;
                return;
            }
            var identity = role == "toolResult" ? PiJson.Text(message, "toolCallId") : PiJson.Field(message, "timestamp").ToString();
            usage[role + ":" + (identity.Length > 0 ? identity : Guid.NewGuid().ToString("N"))] = total.Value;
        }
        else if (type == "compaction_end" && ReadTokens(PiJson.Field(PiJson.Field(packet, "result"), "usage")) is long tokens)
            usage["compaction:" + Guid.NewGuid().ToString("N")] = tokens;
    }

    public RunUsage? Complete()
    {
        if (started is not long timestamp) return null;
        var result = new RunUsage(sawAssistant && !missingAssistantUsage ? usage.Values.Sum() : null, clock.GetElapsedTime(timestamp));
        Reset();
        return result;
    }

    public void Reset()
    {
        started = null;
        usage.Clear();
        missingAssistantUsage = false;
        sawAssistant = false;
    }

    private static long? ReadTokens(JsonElement value)
        => PiTokenUsage.Read(value);
}
