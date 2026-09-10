using System.Text.Json;

namespace PiAgentGui.Utilities;

public static class PiTokenUsage
{
    public static long? Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return null;
        long sum = 0;
        foreach (var key in new[] { "input", "output", "cacheRead", "cacheWrite" })
        {
            var field = PiJson.Field(value, key);
            if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt64(out var count) || count < 0 || count > long.MaxValue - sum) return null;
            sum += count;
        }
        return sum;
    }
}
