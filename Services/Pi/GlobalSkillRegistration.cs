using System.Text.Json.Nodes;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Registers whole local folders in Pi's global skills list without interpreting their contents.</summary>
public sealed class GlobalSkillRegistration(string agentDirectory)
{
    private readonly GlobalConfigurationFile settings = new(Path.Combine(agentDirectory, "settings.json"));
    public async Task RegisterAsync(string folder, CancellationToken token = default)
    {
        var fullPath = Path.GetFullPath(folder);
        if (!Directory.Exists(fullPath)) throw new IOException("The selected skill folder is no longer available.");
        await settings.UpdateAsync(root =>
        {
            if (root["skills"] is not null && root["skills"] is not JsonArray) throw new IOException("The existing skills setting must be an array.");
            var skills = root["skills"] as JsonArray ?? new JsonArray();
            foreach (var item in skills)
            {
                if (item is not JsonValue value || !value.TryGetValue<string>(out var source)) continue;
                if (source.StartsWith('!') || source.StartsWith('-')) continue;
                if (source.StartsWith('~')) source = PiAgentDirectory.Resolve(source, Environment.GetEnvironmentVariable("USERPROFILE"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                if (string.Equals(Path.GetFullPath(source, agentDirectory), fullPath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("This folder is already registered globally.");
            }
            skills.Add(fullPath);
            if (root["skills"] is null) root["skills"] = skills;
        }, token);
    }
}
