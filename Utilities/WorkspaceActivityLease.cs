using System.Text.Json;

namespace PiAgentGui.Utilities;

/// <summary>Coordinates restoration against app-owned agents, Git commands, terminals and scripts across windows.</summary>
public sealed class WorkspaceActivityLease : IDisposable
{
    public static string DirectoryPath => Path.Combine(Path.GetTempPath(), "PiAgentGui-workspace-activity");
    private static string RecoveryDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "checkpoint-recovery");
    private static string RecoveryPath(string session) => Path.Combine(RecoveryDirectory,
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(session))) + ".json");
    public static void MarkRecovery(string session, string id)
    {
        Directory.CreateDirectory(RecoveryDirectory);
        File.WriteAllText(RecoveryPath(session), JsonSerializer.Serialize(new { id }));
    }
    public static string? PendingRecovery(string session)
    {
        var path = RecoveryPath(session);
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("id").GetString();
    }
    public static void ClearRecovery(string session) => File.Delete(RecoveryPath(session));
    private readonly FileStream stream;
    private readonly string? marker;
    private WorkspaceActivityLease(FileStream stream, string? marker) { this.stream = stream; this.marker = marker; }
    public static WorkspaceActivityLease Acquire(bool restoring, string? session = null, bool trackChanges = true)
    {
        Directory.CreateDirectory(DirectoryPath);
        var lockPath = Path.Combine(DirectoryPath, "access.lock");
        if (!File.Exists(lockPath))
        {
            try { using var initial = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite); }
            catch (IOException) when (File.Exists(lockPath)) { }
        }
        FileStream stream;
        try { stream = new FileStream(lockPath, FileMode.Open, restoring ? FileAccess.ReadWrite : FileAccess.Read, restoring ? FileShare.None : FileShare.Read); }
        catch (IOException) { throw new IOException(restoring ? "Finish other app runs, Git commands, and open terminals before restoring files." : "A workspace restoration is in progress. Try again when it finishes."); }
        string? marker = null;
        try
        {
            if (!restoring && trackChanges && Directory.Exists(RecoveryDirectory) && Directory.EnumerateFiles(RecoveryDirectory, "*.json").Any())
                throw new IOException("Inspect pending checkpoint recovery before starting more app work.");
            if (!restoring && trackChanges)
            {
                marker = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(marker, JsonSerializer.Serialize(new { pid = Environment.ProcessId, session }));
                var temporary = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".tmp");
                try { File.WriteAllText(temporary, Guid.NewGuid().ToString("N")); File.Move(temporary, Path.Combine(DirectoryPath, "generation"), true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            return new(stream, marker);
        }
        catch { stream.Dispose(); if (marker is not null) File.Delete(marker); throw; }
    }
    public void Dispose()
    {
        if (marker is not null) { try { File.Delete(marker); } catch (IOException) { } }
        stream.Dispose();
    }
}
