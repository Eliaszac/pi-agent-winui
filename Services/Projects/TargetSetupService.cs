using System.Diagnostics;
using System.Text;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Projects;

/// <summary>Validates existing workspaces or explicitly clones a repository. Never deletes a failed clone.</summary>
public sealed class TargetSetupService(TargetCommandRunner runner)
{
    public async Task<ExecutionTarget> PrepareAsync(ExecutionTarget target, string? repositoryUrl, CancellationToken cancellationToken = default, string? sshSecret = null)
    {
        target = ProjectTargets.Normalize(target);
        var credentials = new SshCredentialStore();
        if (target.HasSshSecret && sshSecret is not null) await new SshCredentialSetup(credentials).SaveAsync(target, sshSecret).ConfigureAwait(false);
        try { return await PrepareCoreAsync(target, repositoryUrl, cancellationToken).ConfigureAwait(false); }
        catch
        {
            if (target.HasSshSecret && sshSecret is not null) { try { credentials.Delete(target.Id); } catch (Exception) { } }
            throw;
        }
    }
    private async Task<ExecutionTarget> PrepareCoreAsync(ExecutionTarget target, string? repositoryUrl, CancellationToken cancellationToken)
    {
        target = ProjectTargets.Normalize(target);
        if (repositoryUrl is not null)
        {
            repositoryUrl = repositoryUrl.Trim();
            if (repositoryUrl.Length == 0 || repositoryUrl.StartsWith('-') || repositoryUrl.Any(char.IsControl))
                throw new ArgumentException("Enter a Git repository URL.");
            if (Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length > 0)
                throw new ArgumentException("Use your Git credential manager instead of embedding credentials in the URL.");
        }
        if (target.IsLocal)
        {
            if (repositoryUrl is null)
            {
                if (!Directory.Exists(target.Path)) throw new DirectoryNotFoundException();
                return target;
            }
            if (Path.Exists(target.Path)) throw new ArgumentException("Choose a new destination folder for the clone.");
            var start = new ProcessStartInfo("git") { WorkingDirectory = Path.GetDirectoryName(target.Path)!,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            foreach (var arg in new[] { "clone", "--", repositoryUrl, target.Path }) start.ArgumentList.Add(arg);
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            start.Environment["GCM_INTERACTIVE"] = "never";
            var result = await TargetCommandRunner.RunProcessAsync(start, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new IOException("Git clone failed. Check your Git credentials and destination; any partial checkout was retained.");
        }
        else
        {
            var path = PosixShell.Quote(target.Path);
            var command = "command -v bash >/dev/null && command -v base64 >/dev/null && command -v setsid >/dev/null && " + (repositoryUrl is null ? $"test -d {path}" :
                $"test ! -e {path} && test ! -L {path} && GIT_TERMINAL_PROMPT=0 GCM_INTERACTIVE=never git clone -- {PosixShell.Quote(repositoryUrl)} {path}");
            var result = await runner.RunAsync(target, command, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new IOException(repositoryUrl is null
                ? "Target validation failed. Check the folder, WSL distribution or SSH alias, authentication and known_hosts. Bash, base64 and setsid are required."
                : "Clone on the target failed. Check connectivity, Git credentials and destination. Any partial checkout was retained.");
        }
        return target;
    }
}
