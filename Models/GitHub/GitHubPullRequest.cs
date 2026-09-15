namespace PiAgentGui.Models.GitHub;

public sealed record GitHubPullRequest(int Number, string Title, Uri Url,
    string? Author = null, bool? IsDraft = null, DateTimeOffset? CreatedAt = null, DateTimeOffset? UpdatedAt = null);
