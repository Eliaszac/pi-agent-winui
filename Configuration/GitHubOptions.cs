using System.Text.Json;

namespace PiAgentGui.Configuration;

public sealed record GitHubOptions(string ClientId, string AppSlug)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
    public Uri? InstallationUri => !string.IsNullOrWhiteSpace(AppSlug) && AppSlug.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
        ? new Uri($"https://github.com/apps/{AppSlug}/installations/new") : null;

    public static GitHubOptions Load()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "GitHubApp.json");
        var options = File.Exists(file) ? JsonSerializer.Deserialize<GitHubOptions>(File.ReadAllText(file)) : null;
        return options ?? new("", "");
    }
}
