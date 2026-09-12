using PiAgentGui.Models.Projects;

namespace PiAgentGui.Utilities;

public static class TargetTerminalLaunch
{
    public static (string Executable, string Arguments) Create(ExecutionTarget target, string? command)
    {
        target = ProjectTargets.Normalize(target);
        var script = "cd -- " + PosixShell.Quote(target.Path) + " || exit $?\n" + (command ?? "exec bash -l");
        string[] args = target.Kind switch
        {
            "wsl" => ["--distribution", target.Host, "--exec", "bash", "-lc", script],
            "ssh" => ["-tt", .. SshLaunchOptions.Arguments(target), "--", target.Host, "bash -lc " + PosixShell.Quote(script)],
            _ => throw new ArgumentException("A remote terminal needs a WSL or SSH target.")
        };
        return (target.Kind == "wsl" ? "wsl.exe" : "ssh.exe", string.Join(" ", args.Select(WindowsCommandLine.Quote)));
    }
}
