using System.Diagnostics;
using System.Text;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Projects;

/// <summary>Lists installed distributions without starting a Linux environment.</summary>
public sealed class WslDistributionDiscovery
{
    public async Task<WslDistributionSnapshot> ReadAsync()
    {
        var names = await ListAsync("--quiet").ConfigureAwait(false);
        var verbose = await ListAsync("--verbose").ConfigureAwait(false);
        return WslDistributionParser.Parse(names, verbose);
    }

    private static async Task<string> ListAsync(string format)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.Unicode, StandardErrorEncoding = Encoding.Unicode
        };
        start.ArgumentList.Add("--list"); start.ArgumentList.Add(format);
        using var process = new Process { StartInfo = start };
        process.Start(); process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await error.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("WSL distribution discovery failed.");
            return await output.ConfigureAwait(false);
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(output, error).ConfigureAwait(false); } catch (Exception) { }
        }
    }
}
