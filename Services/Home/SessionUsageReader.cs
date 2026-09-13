using System.Diagnostics;
using System.Text.Json;
using PiAgentGui.Models.Home;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Home;

/// <summary>Reads only catalog-owned session files. Cache is memory-only and invalidated by file metadata.</summary>
public sealed class SessionUsageReader(PiSessionPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (long Length, DateTime Written, UsageInventory Data)> cache = [];

    public async Task<UsageInventory> ReadAsync(IReadOnlyList<Project> projects, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await Task.Run(() => Read(projects, cancellationToken), cancellationToken); }
        finally { gate.Release(); }
    }

    private UsageInventory Read(IReadOnlyList<Project> projects, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var result = new Dictionary<string, UsageSample>();
        var retained = new HashSet<string>();
        var unavailable = 0;
        var skipped = 0;
        long bytes = 0;
        foreach (var item in projects.SelectMany(project => project.Conversations.Select(conversation => (project, conversation)))
            .OrderBy(item => item.conversation.CreatedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = paths.GetSessionFile(item.project.Id, item.conversation.Id);
            retained.Add(path);
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) { cache.Remove(path); continue; } // Empty drafts have no session file yet.
                UsageInventory inventory;
                if (cache.TryGetValue(path, out var previous) && previous.Length == file.Length && previous.Written == file.LastWriteTimeUtc)
                    inventory = previous.Data;
                else
                {
                    if (file.Length > 64 * 1024 * 1024 || bytes + file.Length > 256 * 1024 * 1024 || clock.Elapsed > TimeSpan.FromSeconds(10))
                    { unavailable++; continue; }
                    bytes += file.Length;
                    inventory = ReadFile(path, item.project.Id, item.conversation.Id, cancellationToken);
                    if (file.Length == new FileInfo(path).Length && inventory.SkippedRecords == 0)
                        cache[path] = (file.Length, file.LastWriteTimeUtc, inventory);
                }
                skipped += inventory.SkippedRecords;
                foreach (var sample in inventory.Samples)
                {
                    // Fork/clone copies retain message identities. Attribute shared history to the oldest saved conversation.
                    result.TryAdd(sample.Key, sample);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { unavailable++; cache.Remove(path); }
        }
        foreach (var stale in cache.Keys.Except(retained).ToArray()) cache.Remove(stale);
        return new(result.Values.ToArray(), unavailable, skipped);
    }

    private static UsageInventory ReadFile(string path, Guid projectId, Guid conversationId, CancellationToken cancellationToken)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);
        var rows = new List<UsageSample>();
        var skipped = 0;
        var lineNumber = 0;
        var efforts = new SessionEffortTracker();
        foreach (var line in BoundedJsonLines.Read(reader, cancellationToken))
        {
            lineNumber++;
            if (line is null) { skipped++; continue; }
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                var effort = efforts.Read(document.RootElement);
                if (SessionUsageParser.Parse(document.RootElement, projectId, conversationId, lineNumber) is { } sample)
                    rows.Add(sample with { Effort = effort });
                if (rows.Count >= 100_000) { skipped++; break; }
            }
            catch (Exception error) when (error is JsonException or FormatException or ArgumentOutOfRangeException) { skipped++; }
        }
        return new(rows, 0, skipped);
    }
}
