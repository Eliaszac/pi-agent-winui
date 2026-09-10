using System.Text.Json;
using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Utilities;

public static class GitHubTokenParser
{
    public static GitHubToken Read(JsonElement root)
    {
        var now = DateTimeOffset.UtcNow;
        return new(root.GetProperty("access_token").GetString()!,
            root.TryGetProperty("expires_in", out var expires) ? now.AddSeconds(expires.GetInt32()) : DateTimeOffset.MaxValue,
            root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
            root.TryGetProperty("refresh_token_expires_in", out var refreshExpires) ? now.AddSeconds(refreshExpires.GetInt32()) : null);
    }
}
