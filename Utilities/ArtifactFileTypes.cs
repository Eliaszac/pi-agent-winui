namespace PiAgentGui.Utilities;

public static class ArtifactFileTypes
{
    public static string? ImageMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return "image/gif";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
    public static string Mime(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif",
        ".pdf" => "application/pdf", ".json" => "application/json", ".html" => "text/html",
        ".md" or ".txt" or ".csv" or ".log" or ".xml" or ".css" or ".js" or ".ts" or ".py" or ".ps1" or ".sh" => "text/plain",
        _ => "application/octet-stream"
    };
    public static bool IsText(string name) => Mime(name).StartsWith("text/", StringComparison.Ordinal) || Mime(name) == "application/json";
}
