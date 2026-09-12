using System.Text.Json;
using PiAgentGui.Models.Docker;

namespace PiAgentGui.Services.Docker;

public sealed class DockerPreferenceStore(string path)
{
    public async Task<DockerPreferences> ReadAsync()
    {
        if (!File.Exists(path)) return DockerPreferences.Empty;
        var settings = JsonSerializer.Deserialize<DockerPreferences>(await File.ReadAllTextAsync(path));
        if (settings is null || settings.Sources is null || settings.Links is null ||
            settings.Sources.Any(source => source is null || source.Target is null || source.Target.Kind is not ("local" or "wsl" or "ssh")) ||
            settings.Links.Any(pair => pair.Value is null)) throw new IOException("The saved Docker configuration is invalid.");
        return settings;
    }

    public async Task WriteAsync(DockerPreferences settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }
}
