using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>Extracts the small protocol subset used by the native conversation projection.</summary>
public static class PiJson
{
    public static JsonElement Field(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    public static string Text(JsonElement element, string name) => Field(element, name) is var value && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    public static bool Flag(JsonElement element, string name) => Field(element, name).ValueKind == JsonValueKind.True;
    public static int Number(JsonElement element, string name) => Field(element, name) is var value && value.TryGetInt32Safe(out var number) ? number : 0;
    private static bool TryGetInt32Safe(this JsonElement element, out int number)
    {
        number = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out number);
    }

    public static string Content(JsonElement content, bool includeImagePlaceholder = true)
    {
        if (content.ValueKind == JsonValueKind.String) return content.GetString()!;
        if (content.ValueKind != JsonValueKind.Array) return "";
        return string.Join("\n", content.EnumerateArray().Select(block => Text(block, "type") switch
        {
            "text" => Text(block, "text"),
            "image" => includeImagePlaceholder ? "[Image]" : "",
            _ => ""
        }).Where(text => text.Length > 0));
    }
}
