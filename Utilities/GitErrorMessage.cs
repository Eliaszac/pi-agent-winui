using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

public static class GitErrorMessage
{
    public static string Format(string error)
    {
        var text = Regex.Replace(error, @"(https?://)[^\s/@]+@", "$1[redacted]@", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?i)(access_token|token|password)=([^\s&]+)", "$1=[redacted]");
        return string.IsNullOrWhiteSpace(text) ? "Git couldn't complete the operation." : text.Trim()[..Math.Min(text.Trim().Length, 1800)];
    }
}
