using System.Diagnostics;
using System.Text;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Projects;

public sealed class SshCredentialSetup(SshCredentialStore store)
{
    public async Task SaveAsync(ExecutionTarget target, string secret)
    {
        target = ProjectTargets.Normalize(target);
        if (target.Kind != "ssh" || !target.HasSshSecret) return;
        string prompt;
        if (target.SshAuthentication == "key")
        {
            if (target.SshKeyPath.Length == 0 || !File.Exists(target.SshKeyPath)) throw new ArgumentException("Choose the existing private key before saving its passphrase.");
            prompt = "Enter passphrase for key '" + target.SshKeyPath + "':";
        }
        else
        {
            // Resolve the approved endpoint locally, so jump-host and unrelated prompts cannot receive this password.
            var start = new ProcessStartInfo("ssh.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            foreach (var arg in new[] { "-G", "--", target.Host }) start.ArgumentList.Add(arg);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await TargetCommandRunner.RunProcessAsync(start, timeout.Token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new IOException("Couldn't resolve the SSH host configuration.");
            var fields = result.Output.Split('\n').Select(line => line.TrimEnd('\r').Split(' ', 2))
                .Where(parts => parts.Length == 2).GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First()[1], StringComparer.OrdinalIgnoreCase);
            if (!fields.TryGetValue("user", out var user) || !fields.TryGetValue("hostname", out var host)) throw new IOException("SSH did not provide the destination user and hostname.");
            if (fields.TryGetValue("hostkeyalias", out var alias) && alias != "none") host = alias;
            prompt = user + "@" + host + "'s password:";
        }
        store.Save(target.Id, prompt, secret);
    }
}
