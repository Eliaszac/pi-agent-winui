using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Forwards a single target loopback port over app-owned standard-I/O connections.</summary>
public sealed class BrowserPreviewTunnel : IDisposable
{
    private readonly ExecutionTarget target;
    private readonly int destination;
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    public int Port { get; }
    public BrowserPreviewTunnel(ExecutionTarget target, int destination)
    {
        if (target.Kind is not ("ssh" or "wsl") || destination is < 1 or > 65535) throw new ArgumentException("Invalid browser forwarding target.");
        this.target = target; this.destination = destination;
        listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try { while (!lifetime.IsCancellationRequested) _ = RelayAsync(await listener.AcceptTcpClientAsync(lifetime.Token)); }
        catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task RelayAsync(TcpClient socket)
    {
        using (socket)
        using (var process = new Process { StartInfo = CreateStartInfo(target, destination) })
        using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
        {
            try
            {
                if (!process.Start()) return;
                var drain = DrainAsync(process.StandardError, cancel.Token);
                using var stream = socket.GetStream();
                var upload = stream.CopyToAsync(process.StandardInput.BaseStream, cancel.Token);
                var download = process.StandardOutput.BaseStream.CopyToAsync(stream, cancel.Token);
                await Task.WhenAny(upload, download);
                cancel.Cancel();
                try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                try { await Task.WhenAll(upload, download, drain); } catch (Exception) { }
            }
            catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or System.ComponentModel.Win32Exception) { }
            finally { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } }
        }
    }
    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[1024];
        while (await reader.ReadAsync(buffer.AsMemory(), token) > 0) { }
    }
    public static ProcessStartInfo CreateStartInfo(ExecutionTarget target, int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        target = ProjectTargets.Normalize(target);
        var start = new ProcessStartInfo(target.Kind == "wsl" ? "wsl.exe" : "ssh.exe")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (target.Kind == "ssh")
        {
            foreach (var arg in SshLaunchOptions.Arguments(target)) start.ArgumentList.Add(arg);
            foreach (var arg in new[] { "-T", "-W", $"127.0.0.1:{port}", "--", target.Host }) start.ArgumentList.Add(arg);
            SshLaunchOptions.ConfigureEnvironment(start.Environment, target);
        }
        else if (target.Kind == "wsl")
        {
            const string relay = "import socket,sys,threading,os\ns=socket.create_connection(('127.0.0.1',int(sys.argv[1])),10)\ns.settimeout(None)\ndef send():\n try:\n  while data:=os.read(0,65536): s.sendall(data)\n finally: s.shutdown(socket.SHUT_WR)\nthreading.Thread(target=send,daemon=True).start()\nwhile data:=s.recv(65536):\n sys.stdout.buffer.write(data)\n sys.stdout.buffer.flush()";
            foreach (var arg in new[] { "--distribution", target.Host, "--exec", "python3", "-u", "-c", relay, port.ToString(System.Globalization.CultureInfo.InvariantCulture) }) start.ArgumentList.Add(arg);
        }
        else throw new ArgumentException("A remote target is required.");
        return start;
    }
    public void Dispose() { lifetime.Cancel(); listener.Stop(); }
}
