using System.Diagnostics;
using System.Text;
using PiAgentGui.Models.Projects;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Projects;

/// <summary>Runs bounded setup commands on an explicit target; SSH uses existing keys and verified host records.</summary>
public sealed class TargetCommandRunner
{
    public static ProcessStartInfo CreateStartInfo(ExecutionTarget target, string command)
    {
        target = ProjectTargets.Normalize(target);
        if (target.IsLocal) throw new ArgumentException("Use a local process runner for local commands.");
        var start = new ProcessStartInfo(target.Kind == "wsl" ? "wsl.exe" : "ssh.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        if (target.Kind == "wsl")
        {
            foreach (var arg in new[] { "--distribution", target.Host, "--exec", "bash", "-lc", command }) start.ArgumentList.Add(arg);
        }
        else
        {
            start.ArgumentList.Add("-T");
            foreach (var arg in SshLaunchOptions.Arguments(target)) start.ArgumentList.Add(arg);
            foreach (var arg in new[] { "--", target.Host, "bash -lc " + PosixShell.Quote(command) }) start.ArgumentList.Add(arg);
            SshLaunchOptions.ConfigureEnvironment(start.Environment, target);
        }
        return start;
    }

    public Task<GitResult> RunAsync(ExecutionTarget target, string command, CancellationToken cancellationToken = default) =>
        RunProcessAsync(CreateStartInfo(target, command), cancellationToken);

    public static async Task<GitResult> RunProcessAsync(ProcessStartInfo start, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("The target command could not start.");
        process.StandardInput.Close();
        var output = ReadAsync(process.StandardOutput, timeout);
        var error = ReadAsync(process.StandardError, timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return new(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new IOException("The target command timed out. Check its result before retrying; it may still be running on the target."); }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(output, error).ConfigureAwait(false); } catch (Exception) { }
        }
    }

    private static async Task<string> ReadAsync(StreamReader reader, CancellationTokenSource lifetime)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), lifetime.Token).ConfigureAwait(false)) > 0)
        {
            if (text.Length + count > 4_000_000) { lifetime.Cancel(); throw new IOException("The target returned too much output."); }
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }
}
