using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Explicit snippet operations on one immutable conversation target, without model interaction.</summary>
public sealed class SnippetService(ExecutionTarget target) : ISnippetService
{
    public string TargetLabel => target.Label;

    public async Task<string> SaveAsync(string label, string code, CancellationToken token)
    {
        using var activity = WorkspaceActivityLease.Acquire(false);
        if (!target.IsLocal) return await RemoteAsync("save", label, code, (_, _) => { }, token);
        if (!Directory.Exists(target.Path)) throw new DirectoryNotFoundException("The workspace root is unavailable.");
        for (var index = 1; index <= 10000; index++)
        {
            token.ThrowIfCancellationRequested();
            var path = Path.Combine(target.Path, SnippetLanguage.FileName(label, index));
            FileStream file;
            try { file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true); }
            catch (IOException) when (Path.Exists(path)) { continue; }
            await using (file) await file.WriteAsync(Encoding.UTF8.GetBytes(code), token);
            return path;
        }
        throw new IOException("Too many snippet files already exist in this workspace.");
    }

    public async Task<int> RunAsync(string label, string code, Action<string, bool> output, CancellationToken token)
    {
        if (!SnippetLanguage.CanRun(label)) throw new ArgumentException("Run supports PowerShell, Bash, and Python.");
        using var activity = WorkspaceActivityLease.Acquire(false);
        if (!target.IsLocal) return int.Parse(await RemoteAsync("run", label, code, output, token), System.Globalization.CultureInfo.InvariantCulture);
        var temporary = Path.Combine(Path.GetTempPath(), "pi-snippet-" + Guid.NewGuid().ToString("N") + SnippetLanguage.Extension(label));
        try
        {
            await File.WriteAllTextAsync(temporary, code, new UTF8Encoding(SnippetLanguage.Normalize(label) == "powershell"), token);
            var start = SnippetProcess.Create(target.Path, label, temporary);
            using var process = new Process { StartInfo = start };
            token.ThrowIfCancellationRequested();
            if (!process.Start()) throw new IOException("The snippet interpreter could not start.");
            process.StandardInput.Close();
            using var reading = new CancellationTokenSource();
            var stdout = PumpAsync(process.StandardOutput, text => output(text, false), reading.Token);
            var stderr = PumpAsync(process.StandardError, text => output(text, true), reading.Token);
            try
            {
                await process.WaitForExitAsync(token);
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2), token);
                return process.ExitCode;
            }
            finally
            {
                try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                reading.Cancel();
                try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
            }
        }
        finally { try { File.Delete(temporary); } catch (IOException) { } }
    }

    private async Task<string> RemoteAsync(string action, string label, string code, Action<string, bool> output, CancellationToken token)
    {
        var helper = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "PiExtensions", "snippet-runner.py"), token);
        var start = TargetCommandRunner.CreateStartInfo(target, "python3 -u -c " + PosixShell.Quote(helper));
        using var process = new Process { StartInfo = start };
        token.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException("The target connection could not start.");
        using var reading = new CancellationTokenSource();
        var diagnostics = new StringBuilder();
        var stderr = PumpAsync(process.StandardError, text => { if (diagnostics.Length < 8192) diagnostics.Append(text); }, reading.Token);
        string? result = null;
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { action, language = SnippetLanguage.Normalize(label), extension = SnippetLanguage.Extension(label), code, path = target.Path }));
            await process.StandardInput.FlushAsync(token);
            while (await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                using var packet = JsonDocument.Parse(line);
                var root = packet.RootElement;
                switch (root.GetProperty("type").GetString())
                {
                    case "stdout": output(root.GetProperty("text").GetString() ?? "", false); break;
                    case "stderr": output(root.GetProperty("text").GetString() ?? "", true); break;
                    case "error": throw new IOException(root.GetProperty("text").GetString());
                    case "result": result = root.GetProperty("value").GetString(); break;
                }
            }
            await process.WaitForExitAsync(token);
            await stderr;
            if (result is null) throw new IOException("The target did not confirm completion. " + diagnostics);
            return result;
        }
        finally
        {
            // The helper watches this input stream and kills its owned process group on Stop or disconnect.
            try
            {
                if (!process.HasExited)
                {
                    await process.StandardInput.WriteLineAsync("stop");
                    process.StandardInput.Close();
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception) { }
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            reading.Cancel();
            try { await stderr; } catch (OperationCanceledException) { }
        }
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> receive, CancellationToken token)
    {
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0) receive(new string(buffer, 0, count));
    }
}
