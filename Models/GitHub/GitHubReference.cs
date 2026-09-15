namespace PiAgentGui.Models.GitHub;

public sealed record GitHubReference(string Repository, int Number, bool IsPullRequest, string Title, string State, string Content = "")
{
    public string Url => $"https://github.com/{Repository}/{(IsPullRequest ? "pull" : "issues")}/{Number}";
    public string Label => $"{Repository} #{Number} · {Title} · {State}";
    public string Summary => $"{(IsPullRequest ? "Pull request" : "Issue")} · {State} · Shared with agent · Bounded snapshot";
    public static GitHubReference? FromUrl(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return null;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 4 || parts[2] is not ("pull" or "issues") || !int.TryParse(parts[3], out var number) || number < 1
            || parts.Take(2).Any(part => part.Length == 0 || part is "." or ".." || !part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))) return null;
        return new(parts[0] + "/" + parts[1], number, parts[2] == "pull", "", "");
    }
}
