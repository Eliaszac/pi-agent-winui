using System.Collections.Concurrent;
using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Conversation-owned files outside the workspace. Deleted records remain as card tombstones.</summary>
public sealed class ArtifactStore(string directory)
{
    public const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public static string ForSession(string session) => session + ".artifacts";
    public Task MarkRemoteAsync(Guid id, CancellationToken token) => Locked(() =>
    {
        var ids = ReadRemoteIds();
        ids.Add(id);
        WriteRemoteIds(ids);
        return true;
    }, token);
    public Task<HashSet<Guid>> RemoteIdsAsync(CancellationToken token) => Locked(ReadRemoteIds, token);
    public Task ForgetRemoteAsync(Guid id, CancellationToken token) => Locked(() =>
    {
        var ids = ReadRemoteIds();
        ids.Remove(id);
        WriteRemoteIds(ids);
        return true;
    }, token);
    private string RemoteMarker()
    {
        Guard(DirectoryPath);
        var marker = DirectoryPath + ".remote";
        foreach (var path in new[] { marker, marker + ".tmp" })
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked artifact markers are unsupported.");
        return marker;
    }
    private HashSet<Guid> ReadRemoteIds()
    {
        var marker = RemoteMarker();
        if (!File.Exists(marker)) return [];
        if (new FileInfo(marker).Length > 200000) throw new IOException("Invalid artifact transfer inventory.");
        return JsonSerializer.Deserialize<HashSet<Guid>>(File.ReadAllText(marker)) ?? throw new IOException("Invalid artifact transfer inventory.");
    }
    private void WriteRemoteIds(HashSet<Guid> ids)
    {
        var marker = RemoteMarker();
        File.WriteAllText(marker + ".tmp", JsonSerializer.Serialize(ids));
        File.Move(marker + ".tmp", marker, true);
    }

    public static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || name != name.Trim() || name.EndsWith('.')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or ".."
            || name.Contains('/') || name.Contains('\\')) throw new IOException("Choose a filename without folders or special characters (up to 180 characters).");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9')
            throw new IOException("This filename is reserved by Windows.");
        return name;
    }

    public Task<IReadOnlyList<ArtifactRecord>> ListAsync(CancellationToken token = default) => Locked<IReadOnlyList<ArtifactRecord>>(() => List(), token);
    private List<ArtifactRecord> List()
    {
        Guard(DirectoryPath);
        if (!Directory.Exists(DirectoryPath)) return [];
        var files = Directory.GetFiles(DirectoryPath, "*.json");
        if (files.Length > 2000) throw new IOException("This conversation exceeds the artifact inventory limit.");
        return files.Select(file =>
        {
            Guard(file);
            var record = JsonSerializer.Deserialize<ArtifactRecord>(File.ReadAllText(file)) ?? throw new IOException("Invalid artifact metadata.");
            if (Path.GetFileNameWithoutExtension(file) != record.Id.ToString("N") || record.Id == Guid.Empty || record.Version == Guid.Empty || record.Size < 0 || record.Size > MaximumBytes || string.IsNullOrEmpty(record.MimeType) || record.Origin is null) throw new IOException("Invalid artifact metadata.");
            ValidateName(record.Name);
            return record;
        }).OrderByDescending(record => record.UpdatedAt).ToList();
    }

    public Task<ArtifactRecord> SaveAsync(string name, byte[] bytes, string mime, string origin, Guid? id = null, string? sourceKey = null, CancellationToken token = default, bool uniqueName = false, bool shared = true, bool requireSharedExisting = false)
        => Locked(() =>
        {
            ValidateName(name);
            if (bytes.Length > MaximumBytes) throw new IOException("Artifacts can be up to 32 MB.");
            var records = List();
            if (uniqueName)
            {
                var requestedName = name;
                for (var number = 2; records.Any(item => !item.Deleted && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); number++)
                    name = ValidateName(Path.GetFileNameWithoutExtension(requestedName) + $" ({number})" + Path.GetExtension(requestedName));
            }
            var previous = id is { } requested ? records.FirstOrDefault(item => item.Id == requested)
                : sourceKey is not null ? records.FirstOrDefault(item => item.SourceKey == sourceKey) : null;
            if (id.HasValue && previous is null) throw new IOException("This artifact does not belong to this conversation.");
            // Screenshot tombstones must not be recreated by reloading Pi history.
            if (sourceKey?.StartsWith("screenshot:", StringComparison.Ordinal) == true && previous is not null) return previous;
            previous ??= records.FirstOrDefault(item => !item.Deleted && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (requireSharedExisting && previous is { Shared: false }) throw new IOException("This artifact belongs to an unsent upload. Choose another name.");
            if (previous?.Deleted == true) throw new IOException("This artifact was deleted. Create it with a new name.");
            if (records.Any(item => item.Id != previous?.Id && !item.Deleted && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new IOException("An artifact already has that name.");
            if (previous is null && records.Count >= 2000) throw new IOException("This conversation has reached 2,000 artifacts.");
            var now = DateTimeOffset.UtcNow;
            var source = previous?.SourceKey?.StartsWith("screenshot:", StringComparison.Ordinal) == true ? previous.SourceKey : sourceKey ?? previous?.SourceKey;
            var next = new ArtifactRecord(previous?.Id ?? Guid.NewGuid(), name, mime, bytes.Length, previous?.CreatedAt ?? now, now, Guid.NewGuid(), origin, source, Shared: previous?.Shared ?? shared);
            var payload = Payload(next);
            Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
            File.WriteAllBytes(payload, bytes);
            try { Write(next); }
            catch { File.Delete(payload); throw; }
            if (previous is not null) RemovePayload(previous);
            return next;
        }, token);

    public Task ShareAsync(IReadOnlyList<Guid> ids, CancellationToken token) => Locked(() =>
    {
        var records = List();
        var selected = ids.Select(id => records.FirstOrDefault(item => item.Id == id && !item.Deleted) ?? throw new IOException("An attached file was deleted or is unavailable. Remove it from the message before sending.")).ToArray();
        foreach (var record in selected.Where(item => !item.Shared)) Write(record with { Shared = true });
        return true;
    }, token);

    public Task<ArtifactRecord> RenameAsync(Guid id, string name) => Locked(() =>
    {
        ValidateName(name);
        var records = List();
        if (records.Any(item => item.Id != id && !item.Deleted && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new IOException("An artifact already has that name.");
        var previous = records.Single(item => item.Id == id);
        if (previous.Deleted) throw new IOException("This artifact was deleted.");
        var next = previous with { Name = name, MimeType = Utilities.ArtifactFileTypes.Mime(name), Version = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow };
        var payload = Payload(next);
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.Copy(Payload(previous), payload);
        try { Write(next); } catch { File.Delete(payload); throw; }
        RemovePayload(previous);
        return next;
    });

    public Task DiscardUploadAsync(Guid id) => Locked(() =>
    {
        var record = List().FirstOrDefault(item => item.Id == id);
        if (record is null || record.Shared || record.SourceKey?.StartsWith("upload:", StringComparison.Ordinal) != true) return false;
        RemovePayload(record);
        var metadata = Path.Combine(DirectoryPath, id.ToString("N") + ".json");
        Guard(metadata);
        File.Delete(metadata);
        return true;
    });

    public Task<ArtifactRecord> DeleteAsync(Guid id) => Locked(() =>
    {
        var previous = List().Single(item => item.Id == id);
        var next = previous with { Deleted = true, UpdatedAt = DateTimeOffset.UtcNow };
        Write(next);
        RemovePayload(previous);
        return next;
    });

    public Task<string> GetPathAsync(Guid id) => Locked(() =>
    {
        var record = List().FirstOrDefault(item => item.Id == id) ?? throw new IOException("Artifact unavailable in this conversation.");
        if (record.Deleted) throw new IOException("This artifact was deleted.");
        var path = Payload(record);
        if (!File.Exists(path)) throw new IOException("The artifact file is missing.");
        return path;
    });

    private string Payload(ArtifactRecord record)
    {
        ValidateName(record.Name);
        var path = Path.Combine(DirectoryPath, record.Id.ToString("N"), record.Version.ToString("N"), record.Name);
        Guard(path);
        return path;
    }
    private void RemovePayload(ArtifactRecord record)
    {
        var path = Payload(record);
        if (File.Exists(path)) File.Delete(path);
        var version = Path.GetDirectoryName(path)!;
        if (Directory.Exists(version) && !Directory.EnumerateFileSystemEntries(version).Any()) Directory.Delete(version);
        var artifact = Path.GetDirectoryName(version)!;
        if (Directory.Exists(artifact) && !Directory.EnumerateFileSystemEntries(artifact).Any()) Directory.Delete(artifact);
    }

    public Task DeleteAllAsync() => Locked(() =>
    {
        Guard(DirectoryPath);
        if (!Directory.Exists(DirectoryPath)) return true;
        var files = new List<string>();
        var folders = new List<string>();
        var pending = new Stack<string>();
        pending.Push(DirectoryPath);
        // Validate the complete tree before touching it, and never follow linked directories.
        while (pending.TryPop(out var folder))
        {
            Guard(folder);
            folders.Add(folder);
            foreach (var child in Directory.EnumerateFileSystemEntries(folder))
            {
                Guard(child);
                if (Directory.Exists(child)) pending.Push(child); else files.Add(child);
            }
        }
        foreach (var file in files) { Guard(file); File.Delete(file); }
        foreach (var folder in folders.AsEnumerable().Reverse()) { Guard(folder); Directory.Delete(folder); }
        return true;
    });

    public Task CopyToAsync(ArtifactStore destination) => Locked(() =>
    {
        var records = List();
        destination.Guard(destination.DirectoryPath);
        if (Directory.Exists(destination.DirectoryPath)) throw new IOException("The copied conversation already has artifact storage.");
        foreach (var record in records)
        {
            if (!record.Deleted)
            {
                var output = destination.Payload(record);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(Payload(record), output);
            }
            destination.Write(record);
        }
        return true;
    });
    private void Write(ArtifactRecord record)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, record.Id.ToString("N") + ".json");
        Guard(path);
        var temporary = path + ".tmp";
        Guard(temporary);
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(record)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void Guard(string path)
    {
        var full = Path.GetFullPath(path);
        if (full != DirectoryPath && !full.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid artifact path.");
        for (var current = full; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked artifact storage is unsupported.");
    }
    private async Task<T> Locked<T>(Func<T> action, CancellationToken token = default)
    {
        var gate = Gates.GetOrAdd(DirectoryPath, _ => new(1, 1));
        await gate.WaitAsync(token);
        try { return await Task.Run(action, token); }
        finally { gate.Release(); }
    }
}
