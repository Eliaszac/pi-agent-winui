using PiAgentGui.Models.Settings;

namespace PiAgentGui.Services.Settings;

/// <summary>Measures explicitly owned local storage without following linked folders.</summary>
public sealed class StorageOverviewService(string appDirectory, string checkpointDirectory)
{
    public string AppDirectory { get; } = Path.GetFullPath(appDirectory);
    public Task<IReadOnlyList<StorageUsage>> ReadAsync() => Task.Run<IReadOnlyList<StorageUsage>>(() =>
    {
        var bytes = new long[5];
        var incomplete = new bool[5];
        Scan(Path.Combine(AppDirectory, "sessions"), path => path.Split(Path.DirectorySeparatorChar).Any(part => part.EndsWith(".artifacts", StringComparison.OrdinalIgnoreCase)) ? 1 : 0, [0, 1]);
        Scan(Path.Combine(AppDirectory, "research"), _ => 2, [2]);
        Scan(checkpointDirectory, _ => 3, [3]);
        Scan(Path.Combine(AppDirectory, "diagnostics"), _ => 4, [4]);
        return new[] { "Conversations", "Artifacts", "Research", "Checkpoints", "Diagnostics" }
            .Select((name, index) => new StorageUsage(name, bytes[index], incomplete[index])).ToArray();

        void Scan(string root, Func<string, int> category, int[] affected)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            var count = 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (pending.TryPop(out var directory))
            {
                try
                {
                    ValidatePath(directory);
                    if (!Directory.Exists(directory)) continue;
                    foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                    {
                        if (++count > 200000 || timer.Elapsed > TimeSpan.FromSeconds(10)) throw new IOException("Scan limit reached.");
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) { foreach (var index in affected) incomplete[index] = true; continue; }
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                        else bytes[category(path)] += new FileInfo(path).Length;
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    foreach (var index in affected) incomplete[index] = true;
                    if (count > 200000 || timer.Elapsed > TimeSpan.FromSeconds(10)) break;
                }
            }
        }
    });

    public Task ClearDiagnosticsAsync() => Task.Run(() =>
    {
        // Only the known report is cleared; unrelated files are never swept.
        var report = Path.Combine(AppDirectory, "diagnostics", "last-ui-crash.txt");
        ValidatePath(report);
        if (File.Exists(report)) File.Delete(report);
    });

    internal static void ValidatePath(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked storage is not supported.");
    }
}
