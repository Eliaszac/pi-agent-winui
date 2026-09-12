using System.Text.Json;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>Stores app-wide favorites without modifying Pi configuration or sessions.</summary>
public sealed class ModelFavoritesStore(string file)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlySet<ModelIdentity>> ReadAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { return await ReadFileAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlySet<ModelIdentity>> SetAsync(ModelIdentity model, bool favorite)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            // Cooperating app instances serialize the entire read/modify/write operation.
            await using var fileLock = await AcquireLockAsync().ConfigureAwait(false);
            var models = await ReadFileAsync().ConfigureAwait(false);
            if (favorite) models.Add(model); else models.Remove(model);
            var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(models.OrderBy(item => item.Provider, StringComparer.Ordinal)
                    .ThenBy(item => item.Id, StringComparer.Ordinal))).ConfigureAwait(false);
                File.Move(temporary, file, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return models;
        }
        finally { gate.Release(); }
    }

    private async Task<HashSet<ModelIdentity>> ReadFileAsync()
    {
        if (!File.Exists(file)) return [];
        if (new FileInfo(file).Length > 1024 * 1024) throw new InvalidDataException("The model favorites file is too large.");
        var models = JsonSerializer.Deserialize<ModelIdentity[]>(await File.ReadAllTextAsync(file).ConfigureAwait(false))
            ?? throw new InvalidDataException("The model favorites file is invalid.");
        if (models.Any(model => model is null || string.IsNullOrWhiteSpace(model.Provider) || string.IsNullOrWhiteSpace(model.Id)))
            throw new InvalidDataException("The model favorites file contains an invalid model.");
        return models.ToHashSet();
    }

    private async Task<FileStream> AcquireLockAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try { return new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33 && DateTime.UtcNow < deadline)
            { await Task.Delay(50).ConfigureAwait(false); }
        }
    }
}
