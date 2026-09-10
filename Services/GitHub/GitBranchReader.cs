using System.Diagnostics;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.GitHub;

/// <summary>Reads local Git metadata only; no fetch, hooks, credentials or working-tree mutations.</summary>
public sealed class GitBranchReader : IGitBranchReader
{
    public async Task<bool> IsRepositoryAsync(string directory, CancellationToken cancellationToken) =>
        await RunAsync(directory, ["rev-parse", "--is-inside-work-tree"], cancellationToken) == "true";

    public async Task<GitHubBranch?> ReadAsync(string directory, CancellationToken cancellationToken)
    {
        var branch = await RunAsync(directory, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken);
        if (string.IsNullOrWhiteSpace(branch)) return null;
        var upstream = await RunAsync(directory, ["for-each-ref", "--format=%(upstream:remotename)%00%(upstream:remoteref)", "refs/heads/" + branch], cancellationToken);
        var fields = upstream?.Split('\0');
        var remote = fields is { Length: 2 } && !string.IsNullOrWhiteSpace(fields[0]) ? fields[0] : "origin";
        if (remote == ".") return null;
        var remoteBranch = fields is { Length: 2 } && fields[1].StartsWith("refs/heads/", StringComparison.Ordinal) ? fields[1][11..] : branch;
        var url = await RunAsync(directory, ["remote", "get-url", "--", remote], cancellationToken);
        // Avoid assigning a completed lookup to a branch switched during the read.
        if (branch != await RunAsync(directory, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken)) return null;
        return url is null ? null : GitHubRemote.Parse(url, remoteBranch);
    }

    private static async Task<string?> RunAsync(string directory, string[] arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await error;
            return process.ExitCode == 0 ? (await output).TrimEnd('\r', '\n') : null;
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
    }
}
