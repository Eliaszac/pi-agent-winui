namespace PiAgentGui.Utilities;

/// <summary>Quotes one literal POSIX shell argument. Never use Windows quoting for remote commands.</summary>
public static class PosixShell
{
    public static string Quote(string value)
    {
        if (value.Contains('\0')) throw new ArgumentException("Shell arguments cannot contain NUL.");
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }
}
