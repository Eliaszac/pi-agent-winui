using System.Diagnostics;
using System.Runtime.CompilerServices;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Owns a hidden Pi process, its streams, and a cross-window session lease.</summary>
public sealed class ProcessPiTransport(PiProcessStartInfoFactory startInfoFactory) : IPiTransport
{
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private Process? process;
    private FileStream? sessionLease;
    private Task stderrTask = Task.CompletedTask;
    private int disposed;
    private readonly PiExitHint exitHint = new();
    public ProcessIdentity? ProcessIdentity { get; private set; }

    public Task StartAsync(PiLaunchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (process is not null) throw new InvalidOperationException("Pi is already started.");
        var info = startInfoFactory.Create(request);
        if (!request.ManageProviders) Directory.CreateDirectory(Path.GetDirectoryName(request.SessionFile)!);
        try
        {
            try { if (!request.ManageProviders) sessionLease = new FileStream(request.SessionFile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException exception) { throw new IOException("This session is in use by another app window, or its storage is unavailable. Disconnect it there before retrying.", exception); }
            process = new Process { StartInfo = info };
            if (!process.Start()) throw new IOException("Pi could not start.");
            ProcessIdentity = new(process.Id, process.StartTime.ToUniversalTime());
            process.StandardInput.NewLine = "\n";
            stderrTask = DrainStderrAsync(process.StandardError);
            return Task.CompletedTask;
        }
        catch
        {
            process?.Dispose();
            process = null;
            sessionLease?.Dispose();
            sessionLease = null;
            throw;
        }
    }

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var running = process ?? throw new InvalidOperationException("Pi is not connected.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await foreach (var line in PiJsonLineReader.ReadAsync(running.StandardOutput, linked.Token).ConfigureAwait(false)) yield return line;
        if (!linked.IsCancellationRequested)
        {
            try { await stderrTask.WaitAsync(TimeSpan.FromMilliseconds(200), linked.Token).ConfigureAwait(false); }
            catch (TimeoutException) { }
            throw new PiProcessExitException(running.HasExited ? running.ExitCode : null, exitHint.Describe());
        }
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (line.Contains('\n') || line.Contains('\r')) throw new ArgumentException("A protocol record must occupy one line.", nameof(line));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await writes.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var running = process ?? throw new IOException("Pi is not connected.");
            await running.StandardInput.WriteLineAsync(line.AsMemory(), linked.Token).ConfigureAwait(false);
            await running.StandardInput.FlushAsync(linked.Token).ConfigureAwait(false);
        }
        finally { writes.Release(); }
    }

    private async Task DrainStderrAsync(StreamReader reader)
    {
        // Drain continuously to prevent pipe deadlocks. Do not persist potentially sensitive diagnostics.
        var buffer = new char[4096];
        try { int count; while ((count = await reader.ReadAsync(buffer.AsMemory(), lifetime.Token).ConfigureAwait(false)) > 0) exitHint.Append(buffer.AsSpan(0, count)); }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        try
        {
            if (process is not null)
            {
                process.StandardInput.BaseStream.Close();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    using var killDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await process.WaitForExitAsync(killDeadline.Token).ConfigureAwait(false);
                }
            }
            await stderrTask.ConfigureAwait(false);
        }
        finally
        {
            process?.Dispose();
            sessionLease?.Dispose();
            lifetime.Dispose();
        }
    }
}
