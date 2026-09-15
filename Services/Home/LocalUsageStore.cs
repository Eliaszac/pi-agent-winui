using System.Collections.Concurrent;
using System.Text.Json;
using PiAgentGui.Models.Home;

namespace PiAgentGui.Services.Home;

/// <summary>Retains only deduplicated response usage metadata, independent of session lifetime.</summary>
public sealed class LocalUsageStore(string path)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string file = Path.GetFullPath(path);

    public IReadOnlyList<UsageSample> Merge(IEnumerable<UsageSample> samples, DateTimeOffset? resetAt, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(file, _ => new(1, 1));
        gate.Wait(cancellationToken);
        try
        {
            var previous = File.Exists(file)
                ? JsonSerializer.Deserialize<UsageArchive>(File.ReadAllText(file)) ?? throw new IOException("Invalid local usage history.")
                : new UsageArchive([]);
            var cutoff = previous.ResetAt is { } saved && (resetAt is null || saved > resetAt) ? saved : resetAt;
            var retained = previous.Samples.Where(sample => cutoff is null || sample.At >= cutoff).ToDictionary(sample => sample.Key);
            foreach (var sample in samples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (cutoff is not null && sample.At < cutoff) continue;
                // Refresh counts without moving already-attributed fork history to a clone.
                retained[sample.Key] = retained.TryGetValue(sample.Key, out var existing)
                    ? sample with { ProjectId = existing.ProjectId, ConversationId = existing.ConversationId } : sample;
            }
            var next = new UsageArchive(retained.Values.ToArray(), cutoff);
            if (previous.ResetAt == next.ResetAt && previous.Samples.SequenceEqual(next.Samples)) return next.Samples;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(next));
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, file, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return next.Samples;
        }
        finally { gate.Release(); }
    }
}
