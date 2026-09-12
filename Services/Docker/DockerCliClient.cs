using System.Diagnostics;
using PiAgentGui.Models.Docker;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Docker;

/// <summary>Uses the source user's Docker CLI and current context; never installs or starts a daemon.</summary>
public sealed class DockerCliClient : IDockerClient
{
    public async Task<bool> IsInstalledAsync(DockerSource source, CancellationToken cancellationToken)
    {
        try { await RunAsync(source, ["--version"], cancellationToken); return true; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return false; }
    }

    public async Task<IReadOnlyList<DockerContainer>> ListAsync(DockerSource source, CancellationToken cancellationToken) =>
        DockerOutput.Parse(source.Id, await RunAsync(source, ["container", "ls", "--all", "--no-trunc", "--format", "{{json .}}"], cancellationToken));

    public async Task SetRunningAsync(DockerSource source, string containerId, bool running, CancellationToken cancellationToken)
    {
        if (!DockerOutput.IsContainerId(containerId)) throw new ArgumentException("Invalid container ID.", nameof(containerId));
        await RunAsync(source, ["container", running ? "start" : "stop", containerId], cancellationToken);
    }

    public static ProcessStartInfo CreateStartInfo(DockerSource source, IReadOnlyList<string> arguments)
    {
        if (!source.Target.IsLocal)
            return TargetCommandRunner.CreateStartInfo(source.Target, "docker " + string.Join(" ", arguments.Select(PosixShell.Quote)));
        var start = new ProcessStartInfo(DockerExecutableLocator.Find())
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    private static async Task<string> RunAsync(DockerSource source, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            // Process creation, SSH setup and pipe reads stay off the UI thread.
            var result = await Task.Run(() => TargetCommandRunner.RunProcessAsync(CreateStartInfo(source, arguments), timeout.Token, trackChanges: false), timeout.Token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new IOException(string.IsNullOrWhiteSpace(result.Error) ? "Docker could not complete the command." : result.Error.Trim()[..Math.Min(2000, result.Error.Trim().Length)]);
            return result.Output;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new IOException("Docker timed out. Check the source connection and refresh its status before retrying."); }
    }
}
