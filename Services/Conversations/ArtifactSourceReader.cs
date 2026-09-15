using System.Diagnostics;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Imports a bounded file from the conversation's execution target, including files outside its workspace.</summary>
public sealed class ArtifactSourceReader
{
    public static string ImportCommand(string path) => new ArtifactTransferFrame().Command(path);
    public async Task<byte[]> ReadAsync(ExecutionTarget target, string path, CancellationToken token)
    {
        if (target.IsLocal)
        {
            if (!Path.IsPathFullyQualified(path)) throw new IOException("Use an absolute Windows file path.");
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            return await ReadBoundedAsync(file, token);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        var frame = new ArtifactTransferFrame();
        using var process = new Process { StartInfo = TargetCommandRunner.CreateStartInfo(target, frame.Command(path)) };
        if (!process.Start()) throw new IOException("Could not connect to the artifact's execution target.");
        process.StandardInput.Close();
        var errors = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            var bytes = await ReadBoundedAsync(process.StandardOutput.BaseStream, timeout.Token, ArtifactStore.MaximumBytes + ArtifactTransferFrame.MaximumOverhead);
            await process.WaitForExitAsync(timeout.Token);
            await errors;
            if (process.ExitCode != 0) throw new IOException("The artifact source is unavailable. Check its path and the WSL/SSH connection.");
            return frame.Decode(bytes);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("Artifact import timed out. Check the WSL/SSH connection."); }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await errors; } catch (Exception) { }
        }
    }
    public static async Task<byte[]> ReadBoundedAsync(Stream source, CancellationToken token, int maximumBytes = ArtifactStore.MaximumBytes)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > maximumBytes) throw new IOException("Artifacts can be up to 32 MB; target startup output must also remain bounded.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
