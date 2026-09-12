using System.Globalization;
using System.Text.Json;
using PiAgentGui.Models.Home;

namespace PiAgentGui.Utilities;

public static class SessionUsageParser
{
    public static UsageSample? Parse(string line, Guid projectId, Guid conversationId, int lineNumber)
    {
        using var document = JsonDocument.Parse(line);
        var entry = document.RootElement;
        if (PiJson.Text(entry, "type") != "message") return null;
        var message = PiJson.Field(entry, "message");
        if (PiJson.Text(message, "role") != "assistant" || PiJson.Text(message, "stopReason") == "pending") return null;
        var timestamp = PiJson.Field(message, "timestamp");
        DateTimeOffset at;
        if (timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var milliseconds))
            at = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        else if (!DateTimeOffset.TryParse(PiJson.Text(entry, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out at))
            throw new FormatException("The usage record has no valid timestamp.");
        var provider = PiJson.Text(message, "provider");
        var model = PiJson.Text(message, "model");
        var id = PiJson.Text(entry, "id");
        var responseId = PiJson.Text(message, "responseId");
        var identity = responseId.Length > 0 ? provider + "/" + responseId
            : id.Length > 0 ? $"{provider}/{model}/{id}/{at.ToUnixTimeMilliseconds()}" : $"{conversationId:N}/{lineNumber}";
        return new(identity, projectId, conversationId, at, provider.Length == 0 ? "Unknown provider" : provider,
            model.Length == 0 ? "Unknown model" : model, PiTokenUsage.Read(PiJson.Field(message, "usage")));
    }
}
