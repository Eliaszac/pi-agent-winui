using System.Text.Json;
using PiAgentGui.Models.Settings;

namespace PiAgentGui.Services.Settings;

/// <summary>Persists only desktop preferences, independently of Pi configuration and sessions.</summary>
public sealed class AppSettingsStore(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public AppPreferences Current { get; private set; } = new();
    public event EventHandler? Changed;

    public async Task LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (File.Exists(path)) Current = JsonSerializer.Deserialize<AppPreferences>(await File.ReadAllTextAsync(path)) ?? new();
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(AppPreferences preferences)
    {
        await gate.WaitAsync();
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, path, overwrite: true);
            Current = preferences;
        }
        finally { gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
