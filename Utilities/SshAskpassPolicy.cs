namespace PiAgentGui.Utilities;

public static class SshAskpassPolicy
{
    public static bool Allows(string expectedPrompt, string actualPrompt, string? hint) =>
        string.IsNullOrEmpty(hint) && expectedPrompt.Length > 0 &&
        (expectedPrompt.StartsWith("Enter passphrase for key '", StringComparison.Ordinal)
            ? string.Equals(expectedPrompt.TrimEnd().Replace('\\', '/'), actualPrompt.TrimEnd().Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
            : string.Equals(expectedPrompt.TrimEnd(), actualPrompt.TrimEnd(), StringComparison.Ordinal));
}
