using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Clears target-owned checkpoint snapshots without touching workspaces or Pi sessions.</summary>
public sealed class CheckpointDataService(IProjectRepository projects)
{
    public static CheckpointDataService CreateDefault() => new(new JsonProjectRepository(new ProjectStorageOptions()));

    public async Task<IReadOnlyList<ExecutionTarget>> GetTargetsAsync()
    {
        var saved = await projects.GetAllAsync();
        return new[] { new ExecutionTarget { Id = Guid.NewGuid(), Name = "This computer", Path = Path.GetTempPath() } }
            .Concat(saved.SelectMany(ProjectTargets.All).Where(target => !target.IsLocal)
                .DistinctBy(target => (target.Kind, target.Host, target.SshAuthentication, target.SshKeyPath)))
            .ToArray();
    }

    public Task<int> ClearAsync(ExecutionTarget target) => ExecuteAsync(target, "clear", "maintenance");
    public Task<int> ForgetAsync(ExecutionTarget target, string sessionFile) => target.IsLocal &&
        !Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi-desktop-checkpoints"))
        ? Task.FromResult(0)
        : ExecuteAsync(target with { Path = target.IsLocal ? Path.GetTempPath() : "/tmp" }, "forget", sessionFile);

    private async Task<int> ExecuteAsync(ExecutionTarget target, string action, string session)
    {
        target = ProjectTargets.Normalize(target);
        using var activity = WorkspaceActivityLease.Acquire(true);
        var code = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "PiExtensions", "checkpoint_engine.py"));
        const string loader = "import sys,base64; exec(compile(base64.b64decode(sys.stdin.readline()),'checkpoint_engine.py','exec'))";
        ProcessStartInfo start;
        if (target.IsLocal)
        {
            start = new("python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
            start.ArgumentList.Add("-X"); start.ArgumentList.Add("utf8");
            start.ArgumentList.Add("-c"); start.ArgumentList.Add(loader);
        }
        else
        {
            var command = "python3 -X utf8 -c " + PosixShell.Quote(loader);
            if (action == "forget") command = "if [ -d \"$HOME/.pi-desktop-checkpoints\" ]; then " + command +
                "; else cat >/dev/null; printf '%s\\n' '{\"ok\":true,\"data\":{\"cleared\":0}}'; fi";
            start = TargetCommandRunner.CreateStartInfo(target, command);
        }
        using var process = new Process { StartInfo = start };
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        if (!process.Start()) throw new IOException("Could not start checkpoint cleanup. Check Python on this target.");
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.StandardInput.WriteLineAsync(Convert.ToBase64String(code).AsMemory(), timeout.Token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { action, root = target.Path, session }).AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0) throw new IOException("Checkpoint cleanup failed. Check the target connection and Python installation.");
            using var response = JsonDocument.Parse(await output);
            if (!PiJson.Flag(response.RootElement, "ok")) throw new IOException(PiJson.Text(response.RootElement, "error"));
            return PiJson.Field(PiJson.Field(response.RootElement, "data"), "cleared").GetInt32();
        }
        catch (OperationCanceledException)
        {
            throw new IOException("Cleanup timed out; some snapshot data may remain. Reconnect and check before retrying.");
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(output, error); } catch (OperationCanceledException) { }
        }
    }
}
