using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>Extracts a browser action from the adapter's bounded OAuth input prompt.</summary>
public static class McpAuthorizationLink
{
    public static Uri? Parse(string text)
    {
        if (text.Length > 32768) return null;
        foreach (Match match in Regex.Matches(text, @"https://[^\s\x1b\x07]+"))
            if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0) return uri;
        return null;
    }
}
