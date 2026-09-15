using System.Diagnostics;
using System.Text;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;

namespace PiAgentGui.Services.Conversations;

public sealed class ArtifactTargetTransport
{
    public async Task<string> RunAsync(ExecutionTarget target, string command, byte[]? input, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var process = new Process { StartInfo = TargetCommandRunner.CreateStartInfo(target, command) };
        if (!process.Start()) throw new IOException("Could not connect to the artifact target.");
        var output = ArtifactSourceReader.ReadBoundedAsync(process.StandardOutput.BaseStream, timeout.Token, 65536);
        var errors = ArtifactSourceReader.ReadBoundedAsync(process.StandardError.BaseStream, timeout.Token, 65536);
        try
        {
            if (input is not null) await process.StandardInput.BaseStream.WriteAsync(input, timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var bytes = await output.ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("Could not access artifact storage on the target. Check the connection, free space, and permissions.");
            return Encoding.UTF8.GetString(bytes);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("Artifact transfer timed out. The local copy is safe; reconnect and retry."); }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(output, errors).ConfigureAwait(false); } catch (Exception) { }
        }
    }
}
