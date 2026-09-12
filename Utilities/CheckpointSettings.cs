using System.Text.Json;

namespace PiAgentGui.Utilities;

public static class CheckpointSettings
{
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "checkpoints.json");
    public static bool IsEnabled() => File.Exists(FilePath) && JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(FilePath))?.GetValueOrDefault("enabled").ValueKind == JsonValueKind.True;
    public static void SetEnabled(bool enabled)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(new { version = 1, enabled })); File.Move(temporary, FilePath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
