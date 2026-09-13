using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Persists command identifiers and usage, never search text or command content.</summary>
public sealed class PaletteUsageStore(string path)
{
    private Dictionary<string, long[]> usage = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task LoadAsync()
    {
        await gate.WaitAsync();
        try { if (File.Exists(path)) usage = JsonSerializer.Deserialize<Dictionary<string, long[]>>(await File.ReadAllTextAsync(path)) ?? []; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { usage = []; }
        finally { gate.Release(); }
    }
    public async Task ClearAsync()
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path + ".tmp", "{}");
            File.Move(path + ".tmp", path, true);
            usage = [];
        }
        finally { gate.Release(); }
    }
    public IReadOnlyList<PaletteCommand> Rank(IEnumerable<PaletteCommand> commands, string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return commands.Where(command => command.CanUse && words.All(word => (command.Title + " " + command.Description + " " + command.Aliases).Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(command => query.Length > 0 && command.Title.Equals(query, StringComparison.OrdinalIgnoreCase) ? 2 : query.Length > 0 && command.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(command => usage.TryGetValue(command.Id, out var data) && data is { Length: 2 } ? data[0] : 0)
            .ThenByDescending(command => usage.TryGetValue(command.Id, out var data) && data is { Length: 2 } ? data[1] : 0).ToArray();
    }
    public async Task RecordAsync(string id)
    {
        await gate.WaitAsync();
        try
        {
            var count = usage.TryGetValue(id, out var value) && value is { Length: 2 } ? Math.Max(0, value[0]) : 0;
            usage[id] = [count + 1, DateTimeOffset.UtcNow.UtcTicks];
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(usage));
            File.Move(path + ".tmp", path, true);
        }
        finally { gate.Release(); }
    }
}

