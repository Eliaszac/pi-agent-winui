using System.Diagnostics;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.GitHub;

public sealed class ProjectGitHubRepository
{
    public async Task<string> ReadAsync(ExecutionTarget target, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var start = CreateStartInfo(target);
        var result = await TargetCommandRunner.RunProcessAsync(start, timeout.Token, trackChanges: false);
        var repository = result.ExitCode == 0 ? GitHubRemote.Parse(result.Output.Trim(), "HEAD") : null;
        return repository?.FullName ?? throw new IOException("This project needs a GitHub origin remote to attach PRs or issues.");
    }

    internal static ProcessStartInfo CreateStartInfo(ExecutionTarget target)
    {
        ProcessStartInfo start;
        if (target.IsLocal)
        {
            start = new("git") { WorkingDirectory = target.Path, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "remote", "get-url", "origin" }) start.ArgumentList.Add(arg);
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        }
        else start = TargetCommandRunner.CreateStartInfo(target, "cd -- " + PosixShell.Quote(target.Path) + " && GIT_TERMINAL_PROMPT=0 git remote get-url origin");
        return start;
    }
}
