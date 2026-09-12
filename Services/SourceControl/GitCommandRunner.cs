using System.Diagnostics;
using System.Text;
using PiAgentGui.Models.SourceControl;

namespace PiAgentGui.Services.SourceControl;

/// <summary>Executes Git directly with literal arguments and bounded output; never invokes a shell.</summary>
public sealed class GitCommandRunner : IGitCommandRunner
{
    public async Task<GitResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var activity = Utilities.WorkspaceActivityLease.Acquire(false, trackChanges: Utilities.GitWorkspaceActivity.MayWrite(arguments));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--no-pager"); start.ArgumentList.Add("--literal-pathspecs");
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("color.ui=false");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "never";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["GIT_MERGE_AUTOEDIT"] = "no";
        start.Environment["LC_ALL"] = "C";
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("Git could not start.");
        process.StandardInput.Close();
        var output = ReadBoundedAsync(process.StandardOutput, timeout);
        var error = ReadBoundedAsync(process.StandardError, timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return new(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("Git timed out. Refresh to check the result before retrying.");
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            try { await Task.WhenAll(output, error).ConfigureAwait(false); } catch (Exception) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationTokenSource cancellation)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false)) > 0)
        {
            if (text.Length + count > 4_000_000) { cancellation.Cancel(); throw new IOException("Git returned too much output to display."); }
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }
}
