using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

public static class InstructionSnapshotParser
{
    public const string StatusKey = "pi-gui-instructions-v1";
    public static InstructionSnapshot? Parse(string text)
    {
        if (text.Length > 524288) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var files = PiJson.Field(root, "files");
            if (PiJson.Number(root, "version") != 1 || files.ValueKind != JsonValueKind.Array || files.GetArrayLength() > 256) return null;
            var result = new List<LoadedInstruction>();
            foreach (var file in files.EnumerateArray())
            {
                var path = PiJson.Text(file, "path");
                var hash = PiJson.Text(file, "hash");
                if (path.Length > 4096 || !Path.IsPathFullyQualified(path) || hash.Length != 64 || !hash.All(Uri.IsHexDigit)) return null;
                result.Add(new(path, hash));
            }
            return new(PiJson.Flag(root, "available"), result.DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException) { return null; }
    }
}
