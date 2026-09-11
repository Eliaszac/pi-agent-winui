using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Stores confirmed selector preferences without modifying Pi-owned session history.</summary>
public sealed class ConversationSettingsStore(string sessionFile)
{
    private readonly string path = sessionFile + ".settings.json";

    public async Task<ConversationSettings?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ConversationSettings>(stream, cancellationToken: cancellationToken);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task SaveAsync(ConversationSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
