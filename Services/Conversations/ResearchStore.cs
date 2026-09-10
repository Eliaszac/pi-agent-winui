using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Stores app-owned worker results and the global opt-in, outside project folders.</summary>
public sealed class ResearchStore(string directory)
{
    public string PreferencePath => Path.Combine(directory, "research-enabled.json");
    public bool Enabled => File.Exists(PreferencePath) && JsonSerializer.Deserialize<bool>(File.ReadAllText(PreferencePath));
    public async Task SetEnabledAsync(bool enabled)
    {
        Directory.CreateDirectory(directory);
        await WriteAsync(PreferencePath, JsonSerializer.Serialize(enabled));
    }
    public async Task<List<ResearchTask>> LoadAsync()
    {
        var path = Path.Combine(directory, "research-tasks.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<List<ResearchTask>>(await File.ReadAllTextAsync(path)) ?? [] : [];
    }
    public async Task SaveAsync(IEnumerable<ResearchTask> tasks)
    {
        Directory.CreateDirectory(directory);
        await WriteAsync(Path.Combine(directory, "research-tasks.json"), JsonSerializer.Serialize(tasks));
    }
    private static async Task WriteAsync(string path, string content)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, content);
        File.Move(temporary, path, true);
    }
}
