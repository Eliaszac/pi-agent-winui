using System.Text.RegularExpressions;
using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Utilities;

public static partial class GitHubRemote
{
    public static GitHubBranch? Parse(string remote, string branch)
    {
        string path;
        if (remote.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase)) path = remote[15..];
        else if (Uri.TryCreate(remote, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.Scheme is "https" or "ssh" && uri.IsDefaultPort && uri.Query.Length == 0 && uri.Fragment.Length == 0)
            path = uri.AbsolutePath.TrimStart('/');
        else return null;
        path = path.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        var parts = path.Split('/');
        return parts.Length == 2 && parts.All(part => NamePattern().IsMatch(part) && part is not "." and not "..") && !string.IsNullOrWhiteSpace(branch)
            ? new(parts[0], parts[1], branch) : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex NamePattern();
}
