namespace PiAgentGui.Utilities;

/// <summary>Matches Pi's Windows home-directory and tilde resolution.</summary>
public static class PiAgentDirectory
{
    public static string Resolve(string? configured, string? userProfile, string fallbackProfile)
    {
        var home = string.IsNullOrWhiteSpace(userProfile) ? fallbackProfile : userProfile;
        if (string.IsNullOrEmpty(configured)) return Path.GetFullPath(Path.Combine(home, ".pi", "agent"));
        if (configured == "~") return Path.GetFullPath(home);
        if (configured.StartsWith("~/", StringComparison.Ordinal) || configured.StartsWith("~\\", StringComparison.Ordinal))
            return Path.GetFullPath(Path.Combine(home, configured[2..]));
        return Path.GetFullPath(configured);
    }
}
