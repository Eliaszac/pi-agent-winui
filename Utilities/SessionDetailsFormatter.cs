using System.Globalization;
using System.Text.Json;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Utilities;

public static class SessionDetailsFormatter
{
    public static string Format(JsonElement data, PiModel? model)
    {
        var tokens = PiJson.Field(data, "tokens");
        var context = PiJson.Field(data, "contextUsage");
        return $"Model: {model?.Name ?? "Unavailable"}\nProvider: {model?.Provider ?? "Unavailable"}\n\n" +
            $"User messages: {Number(data, "userMessages")}\nAssistant messages: {Number(data, "assistantMessages")}\nTool calls: {Number(data, "toolCalls")}\n\n" +
            $"Input tokens: {Number(tokens, "input")}\nOutput tokens: {Number(tokens, "output")}\nCache read: {Number(tokens, "cacheRead")}\nCache write: {Number(tokens, "cacheWrite")}\nTotal tokens: {Number(tokens, "total")}\n" +
            $"Reported cost (USD): {Number(data, "cost", "0.####")}\n\n" +
            $"Context estimate: {Number(context, "tokens")} / {Number(context, "contextWindow")} tokens\nContext used (%): {Number(context, "percent", "0.#")}\n\n" +
            "Usage totals include the full session. Context usage may be unavailable until a response completes.";
    }

    private static string Number(JsonElement data, string name, string format = "N0") =>
        PiJson.Field(data, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetDouble(out var number)
            ? number.ToString(format, CultureInfo.CurrentCulture) : "Unavailable";
}
