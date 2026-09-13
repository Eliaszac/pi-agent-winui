using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Utilities;

public static class PullRequestDetailsFormatter
{
    public static string Heading(GitHubPullRequest pull) => $"PR #{pull.Number} · {(pull.IsDraft == true ? "Draft" : "Open")}";

    public static string Opened(GitHubPullRequest pull, DateTimeOffset now)
    {
        var author = string.IsNullOrWhiteSpace(pull.Author) ? null : $"@{pull.Author}";
        var age = pull.CreatedAt is { } created ? RelativeTime(created, now) : null;
        return (author, age) switch
        {
            (not null, not null) => $"Opened by {author} · {age}",
            (not null, null) => $"Opened by {author}",
            (null, not null) => $"Opened {age}",
            _ => ""
        };
    }

    public static string Updated(GitHubPullRequest pull, DateTimeOffset now) =>
        pull.UpdatedAt is { } updated ? $"Updated {RelativeTime(updated, now)}" : "";

    public static string RelativeTime(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var age = now - timestamp;
        if (age.TotalMinutes < 1) return "just now";
        var (count, unit) = age.TotalHours < 1 ? ((int)age.TotalMinutes, "minute")
            : age.TotalDays < 1 ? ((int)age.TotalHours, "hour") : ((int)age.TotalDays, "day");
        return $"{count} {unit}{(count == 1 ? "" : "s")} ago";
    }
}
