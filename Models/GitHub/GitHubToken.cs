namespace PiAgentGui.Models.GitHub;

public sealed record GitHubToken(string AccessToken, DateTimeOffset ExpiresAt, string? RefreshToken = null, DateTimeOffset? RefreshExpiresAt = null);
