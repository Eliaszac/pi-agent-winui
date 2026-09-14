namespace PiAgentGui.Utilities;

public static class BrowserAddress
{
    public static Uri DefaultPage { get; } = new("https://www.google.com/");

    public static Uri FromInput(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return DefaultPage;
        if (value.Contains("://", StringComparison.Ordinal) || value.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return Parse(value);
        if (!value.Any(char.IsWhiteSpace) && Uri.TryCreate("http://" + value, UriKind.Absolute, out var candidate)
            && (candidate.Host.Contains('.') || candidate.IsLoopback || System.Net.IPAddress.TryParse(candidate.Host.Trim('[', ']'), out _)
                || candidate.Authority.Contains(':')))
            return Parse(value);
        return new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    public static Uri Parse(string value)
    {
        value = value.Trim();
        if (value == "about:blank") return new(value);
        if (!value.Contains("://", StringComparison.Ordinal)) value = "http://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0)
            throw new ArgumentException("Enter an HTTP or HTTPS address without embedded credentials.");
        return uri;
    }
}
