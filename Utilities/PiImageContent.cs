using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

public static class PiImageContent
{
    public const int MaximumImages = 4;
    public const int MaximumImageBytes = 2 * 1024 * 1024;

    public static JsonArray Serialize(IReadOnlyList<ChatImage> images)
    {
        if (images.Count > MaximumImages) throw new ArgumentException("Attach up to four screenshots per message.");
        var result = new JsonArray();
        foreach (var image in images)
        {
            if (image.MimeType is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif"))
                throw new ArgumentException("Unsupported screenshot format.");
            if (image.Data.Length > (MaximumImageBytes + 2) / 3 * 4 || Convert.FromBase64String(image.Data).Length > MaximumImageBytes)
                throw new ArgumentException("Each screenshot must be smaller than 2 MB.");
            result.Add(new JsonObject { ["type"] = "image", ["data"] = image.Data, ["mimeType"] = image.MimeType });
        }
        return result;
    }

    public static IReadOnlyList<ChatImage> Read(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Array) return [];
        return content.EnumerateArray()
            .Where(item => PiJson.Text(item, "type") == "image" && PiJson.Text(item, "data").Length > 0)
            .Select(item => new ChatImage(PiJson.Text(item, "data"), PiJson.Text(item, "mimeType")))
            .ToArray();
    }
}
