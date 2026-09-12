using PiAgentGui.Models.Projects;

namespace PiAgentGui.Utilities;

public static class SshLaunchOptions
{
    public static IReadOnlyList<string> Arguments(ExecutionTarget target)
    {
        var args = new List<string> { "-o", target.HasSshSecret ? "BatchMode=no" : "BatchMode=yes", "-o", "NumberOfPasswordPrompts=1",
            "-o", "StrictHostKeyChecking=yes", "-o", "ConnectTimeout=10", "-o", "ServerAliveInterval=15", "-o", "ServerAliveCountMax=2",
            "-o", "KbdInteractiveAuthentication=no", "-o", "AddKeysToAgent=no" };
        args.AddRange(target.SshAuthentication == "password" ? ["-o", "PreferredAuthentications=password", "-o", "PubkeyAuthentication=no", "-o", "PasswordAuthentication=yes"]
            : new[] { "-o", "PreferredAuthentications=publickey", "-o", "PasswordAuthentication=no" });
        if (target.SshAuthentication == "key" && target.SshKeyPath.Length > 0) args.AddRange(["-o", "IdentitiesOnly=yes", "-i", target.SshKeyPath]);
        return args;
    }
    public static void ConfigureEnvironment(IDictionary<string, string?> environment, ExecutionTarget target, string? helper = null)
    {
        if (target.Kind != "ssh" || !target.HasSshSecret) return;
        environment["SSH_ASKPASS"] = helper ?? Path.Combine(AppContext.BaseDirectory, "PiAgentGui.exe");
        environment["SSH_ASKPASS_REQUIRE"] = "force";
        environment["DISPLAY"] = "pi-desktop";
        environment["PI_DESKTOP_SSH_CREDENTIAL"] = target.Id.ToString("N");
    }
}
