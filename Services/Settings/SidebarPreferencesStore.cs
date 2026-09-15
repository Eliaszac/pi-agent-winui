using System.Text.Json;
using PiAgentGui.Models.Settings;

namespace PiAgentGui.Services.Settings;

/// <summary>Persists deliberate sidebar choices separately from transient window layout.</summary>
public sealed class SidebarPreferencesStore(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private SidebarPreferences? pending;
    private SidebarPreferences? saved;

    public async Task<SidebarPreferences> LoadAsync()
        => saved = File.Exists(path)
            ? JsonSerializer.Deserialize<SidebarPreferences>(await File.ReadAllTextAsync(path)) ?? new()
            : new();

    public async Task SaveAsync(SidebarPreferences preferences)
    {
        pending = preferences;
        await gate.WaitAsync();
        try
        {
            var latest = pending;
            if (latest == saved) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(latest));
                File.Move(temporary, path, true);
                saved = latest;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { gate.Release(); }
    }
}
