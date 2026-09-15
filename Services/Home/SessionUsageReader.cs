using System.Diagnostics;
using System.Text.Json;
using PiAgentGui.Models.Home;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Home;

/// <summary>Reads catalog-owned sessions and preserves deduplicated usage metadata outside session storage.</summary>
public sealed class SessionUsageReader(PiSessionPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (long Length, DateTime Written, UsageInventory Data)> cache = [];
    private readonly LocalUsageStore archive = new(paths.UsageArchiveFile);

    public async Task<UsageInventory> ReadAsync(IReadOnlyList<Project> projects, CancellationToken cancellationToken = default, DateTimeOffset? resetAt = null)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await Task.Run(() =>
        {
            var current = Read(projects, cancellationToken);
            return current with { Samples = archive.Merge(current.Samples, resetAt, cancellationToken) };
        }, cancellationToken); }
        finally { gate.Release(); }
    }

    public Task ResetAsync(DateTimeOffset cutoff) => Task.Run(() => archive.Merge([], cutoff, CancellationToken.None));

    public Task PreserveAsync(Guid project, Guid conversation) => Task.Run(() =>
    {
        var file = paths.GetSessionFile(project, conversation);
        if (!File.Exists(file)) return;
        if (new FileInfo(file).Length > 64 * 1024 * 1024)
            throw new IOException("Session is too large to preserve usage safely. Cleanup will retry.");
        var inventory = ReadFile(file, project, conversation, CancellationToken.None);
        archive.Merge(inventory.Samples, null, CancellationToken.None);
    });

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
