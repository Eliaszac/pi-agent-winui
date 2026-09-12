using System.Diagnostics;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Passes a token on stdin to the adapter's supported OS credential-store CLI.</summary>
public sealed class McpTokenStore(PiInstallationLocator locator, string agentDirectory, string workingDirectory, CancellationToken shutdown = default)
{
    public async Task SaveAsync(string name, string secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 16384 || secret.Any(char.IsControl)) throw new ArgumentException("Enter a valid token without line breaks.");
        var installation = locator.Resolve();
        var node = installation.CliPath is not null ? installation.ExecutablePath :
            (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(path => Path.Combine(path.Trim('"'), "node.exe")).FirstOrDefault(File.Exists);
        if (node is null) throw new InvalidOperationException("Node.js is required to save an MCP token.");
        var cli = Path.Combine(agentDirectory, "npm", "node_modules", "pi-mcp-adapter", "cli.js");
        if (!File.Exists(cli)) throw new InvalidOperationException("Install MCP Adapter from Extensions first.");
        Directory.CreateDirectory(workingDirectory);
        var info = new ProcessStartInfo(node) { WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(cli); info.ArgumentList.Add("token"); info.ArgumentList.Add("set"); info.ArgumentList.Add(name);
        info.Environment["PI_CODING_AGENT_DIR"] = agentDirectory;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(shutdown, cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(30));
        using var process = new Process { StartInfo = info };
        lifetime.Token.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException("The credential helper could not start.");
        var output = DrainAsync(process.StandardOutput, lifetime.Token);
        var error = DrainAsync(process.StandardError, lifetime.Token);
        try
        {
            await process.StandardInput.WriteAsync(secret.AsMemory(), lifetime.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(lifetime.Token);
            await Task.WhenAll(output, error);
            if (process.ExitCode != 0) throw new IOException("The adapter could not save the token in the OS credential store. The server configuration remains saved; retry token setup.");
        }
        finally
        {
            if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } }
            lifetime.Cancel();
            try { await Task.WhenAll(output, error); } catch (OperationCanceledException) { }
        }
    }
    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[2048];
        while (await reader.ReadAsync(buffer.AsMemory(), token) > 0) { }
    }
}
