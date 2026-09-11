using System.Diagnostics;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Runs an explicitly confirmed global Pi install independently of conversation runtimes.</summary>
public sealed class PiPackageInstaller(PiInstallationLocator locator, string agentDirectory, CancellationToken shutdown = default)
{
    public ProcessStartInfo CreateStartInfo(string source)
    {
        source = PiPackageSource.Validate(source);
        var installation = locator.Resolve();
        var info = new ProcessStartInfo(installation.ExecutablePath)
        {
            WorkingDirectory = agentDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        if (installation.CliPath is not null) info.ArgumentList.Add(installation.CliPath);
        info.ArgumentList.Add("install"); info.ArgumentList.Add(source);
        info.Environment["PI_CODING_AGENT_DIR"] = agentDirectory;
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return info;
    }

    public async Task InstallAsync(string source, CancellationToken token)
    {
        var info = CreateStartInfo(source);
        Directory.CreateDirectory(agentDirectory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, shutdown);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        using var process = new Process { StartInfo = info };
        timeout.Token.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException("Pi's package installer could not start.");
        process.StandardInput.Close();
        var output = DrainAsync(process.StandardOutput, timeout.Token);
        var error = DrainAsync(process.StandardError, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error);
            if (process.ExitCode != 0) throw new IOException($"Pi installation failed (exit code {process.ExitCode}). Run the displayed command in a terminal for details.");
        }
        finally
        {
            if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
            try { await Task.WhenAll(output, error); } catch (OperationCanceledException) { }
        }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), token) > 0) { }
    }
}
