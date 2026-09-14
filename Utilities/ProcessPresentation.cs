namespace PiAgentGui.Utilities;

/// <summary>General executable roles, not claims about a process's current task or authenticity.</summary>
public sealed record ProcessPresentation(string Title, string Description, string Glyph, bool IsWindowsHelper = false)
{
    public string Category => IsWindowsHelper ? "Windows helper" : "Subprocess";

    public static ProcessPresentation ForExecutable(string name) => name.ToLowerInvariant() switch
    {
        "conhost.exe" => new("Windows Console Host", "Windows helper for console applications.", "\uE770", true),
        "cmd.exe" => new("Command Prompt", "Windows shell for running commands and scripts.", "\uE756"),
        "powershell.exe" => new("Windows PowerShell", "Shell for running PowerShell commands and scripts.", "\uE756"),
        "pwsh.exe" => new("PowerShell", "Shell for running PowerShell commands and scripts.", "\uE756"),
        "node.exe" => new("Node.js", "JavaScript runtime used by tools, servers, and extensions.", "\uE943"),
        "python.exe" or "python3.exe" or "pythonw.exe" => new("Python", "Python runtime for scripts and tools.", "\uE943"),
        "git.exe" => new("Git", "Version-control command-line tool.", "\uE8D4"),
        "bash.exe" => new("Bash", "Shell for running commands and scripts.", "\uE756"),
        "wsl.exe" => new("Windows Subsystem for Linux", "Launches or communicates with a Linux environment.", "\uE756"),
        "ssh.exe" => new("SSH client", "Client for remote commands, connections, and tunnels.", "\uE968"),
        _ => new(name, "Subprocess associated with this conversation. Its specific task is unavailable.", "\uE713")
    };
}
