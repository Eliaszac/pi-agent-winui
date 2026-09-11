using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>Accepts package source arguments, never arbitrary command lines.</summary>
public static partial class PiPackageSource
{
    public static string Validate(string source)
    {
        source = source.Trim();
        if (source.Length is 0 or > 2048 || source.Any(char.IsWhiteSpace) || source.Any(char.IsControl))
            throw new ArgumentException("Enter one npm: or Git package source, not a terminal command.");
        if (NpmSource().IsMatch(source)) return source;
        var remote = source.StartsWith("git:", StringComparison.Ordinal) ? source[4..] : source;
        if (remote.StartsWith("git@", StringComparison.Ordinal)) remote = "ssh://" + remote.Replace(':', '/');
        else if (!remote.Contains("://", StringComparison.Ordinal) && source.StartsWith("git:", StringComparison.Ordinal)) remote = "https://" + remote;
        if (Uri.TryCreate(remote, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "ssh" && uri.Host.Length > 0 && uri.AbsolutePath.Trim('/').Contains('/') &&
            (uri.UserInfo.Length == 0 || uri.Scheme == "ssh" && uri.UserInfo == "git")) return source;
        throw new ArgumentException("Use npm:package, npm:@scope/package, or a Git source such as git:github.com/owner/repository. Keep credentials out of the source.");
    }

    [GeneratedRegex(@"^npm:(?:@[a-z0-9_.-]+/)?[a-z0-9][a-z0-9_.-]*(?:@[a-zA-Z0-9.*^~+_-]+)?$")]
    private static partial Regex NpmSource();
}
