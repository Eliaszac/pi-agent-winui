using System.Text.Json;

namespace PiAgentGui.Services.Applications;

public sealed class OpenInPreferenceStore(string file)
{
    public string? Read()
    {
        if (!File.Exists(file)) return null;
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        return json.RootElement.TryGetProperty("preferredEditor", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public void Save(string id)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { preferredEditor = id }));
            File.Move(temporary, file, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
