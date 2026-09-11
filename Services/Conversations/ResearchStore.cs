using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Stores app-owned worker results and the global opt-in, outside project folders.</summary>
public sealed class ResearchStore(string directory)
{
    /// <summary>Holds exclusive coordinator ownership until all workers have stopped and saved.</summary>
    public FileStream AcquireOwnership()
    {
        Directory.CreateDirectory(directory);
        // Keep the lock file: deleting it could race with the next owner opening it.
        try { return new FileStream(Path.Combine(directory, "research.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException exception)
        {
            throw new IOException("Research storage could not be locked. Another Pi desktop window may own it. Close that window and restart this one; if the problem remains, check access to the app data folder.", exception);
        }
    }
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
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content);
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }
}
