using System.Text;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Projects;

public static class SshAskpass
{
    public static int Run(string[] args)
    {
        try
        {
            if (!Console.IsOutputRedirected || args.Length != 1 || !Guid.TryParseExact(Environment.GetEnvironmentVariable("PI_DESKTOP_SSH_CREDENTIAL"), "N", out var id)) return 1;
            var saved = new SshCredentialStore().Read(id);
            if (saved is null || !SshAskpassPolicy.Allows(saved.Value.Prompt, args[0], Environment.GetEnvironmentVariable("SSH_ASKPASS_PROMPT"))) return 1;
            var bytes = Encoding.UTF8.GetBytes(saved.Value.Secret + "\n");
            try { using var output = Console.OpenStandardOutput(); output.Write(bytes); output.Flush(); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
            return 0;
        }
        catch (Exception) { return 1; }
    }
}
