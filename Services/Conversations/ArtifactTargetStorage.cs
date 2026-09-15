using System.Security.Cryptography;
using System.Text;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Creates disposable working copies on remote targets; Windows artifacts remain authoritative.</summary>
public sealed class ArtifactTargetStorage(ArtifactStore store, ExecutionTarget target,
    Func<ExecutionTarget, string, byte[]?, CancellationToken, Task<string>>? run = null)
{
    private readonly Func<ExecutionTarget, string, byte[]?, CancellationToken, Task<string>> runCommand = run ?? new ArtifactTargetTransport().RunAsync;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    public static string OwnerKey(string directory) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant()))).ToLowerInvariant();
    public string Marker => store.DirectoryPath + ".remote";
    private string Setup => SetupCommand(OwnerKey(store.DirectoryPath));
    public static string SetupCommand(string owner)
    {
        if (owner.Length != 64 || owner.Any(character => !Uri.IsHexDigit(character))) throw new IOException("Invalid artifact storage owner.");
        return "set -eu; case \"$HOME\" in /*) ;; *) exit 1;; esac; "
            + "root=\"$HOME/.pi-desktop-artifacts\"; test ! -L \"$root\"; "
            + "owner=\"$root/" + owner + "\"; test ! -L \"$owner\"; ";
    }
    public static string SaveCommand(string owner, ArtifactRecord record, string replyMarker)
    {
        ArtifactStore.ValidateName(record.Name);
        if (replyMarker.Any(character => !char.IsAsciiLetterOrDigit(character))) throw new IOException("Invalid transfer marker.");
        return SetupCommand(owner) + "umask 077; mkdir -p -- \"$owner\"; "
            + "item=\"$owner/" + record.Id.ToString("N") + "\"; test ! -L \"$item\"; mkdir -p -- \"$item\"; "
            + "version=\"$item/" + record.Version.ToString("N") + "\"; test ! -L \"$version\"; mkdir -p -- \"$version\"; "
            + "file=\"$version/\"" + PosixShell.Quote(record.Name) + "; test ! -L \"$file\"; "
            + "temp=$(mktemp \"$version/.upload-XXXXXX\"); trap 'rm -f -- \"$temp\"' EXIT; cat > \"$temp\"; "
            + "test \"$(wc -c < \"$temp\")\" -eq " + record.Size + "; mv -f -- \"$temp\" \"$file\"; "
            + "for old in \"$item\"/*; do if [ \"$old\" != \"$version\" ]; then test ! -L \"$old\"; rm -rf -- \"$old\"; fi; done; "
            + "printf '\\n" + replyMarker + ":%s\\n' \"$file\"";
    }
    public async Task<string> MaterializeAsync(ArtifactRecord record, CancellationToken token)
    {
        if (target.IsLocal) return await store.GetPathAsync(record.Id);
        var gate = Gates.GetOrAdd(store.DirectoryPath, _ => new(1, 1));
        await gate.WaitAsync(token);
        try
        {
            var current = (await store.ListAsync(token)).FirstOrDefault(item => item.Id == record.Id && !item.Deleted) ?? throw new IOException("Artifact unavailable.");
            var path = await store.GetPathAsync(current.Id);
            byte[] bytes;
            await using (var file = File.OpenRead(path)) bytes = await ArtifactSourceReader.ReadBoundedAsync(file, token);
            await store.MarkRemoteAsync(current.Id, token);
            var staged = await store.RemoteIdsAsync(token);
            foreach (var deleted in (await store.ListAsync(token)).Where(item => item.Deleted && staged.Contains(item.Id))) await DeleteCoreAsync(deleted.Id, token);
            var marker = "Artifact" + Guid.NewGuid().ToString("N");
            var output = await runCommand(target, SaveCommand(OwnerKey(store.DirectoryPath), current with { Size = bytes.Length }, marker), bytes, token);
            if (!(await store.ListAsync(token)).Any(item => item.Id == current.Id && item.Version == current.Version && !item.Deleted))
            {
                await DeleteCoreAsync(current.Id, token);
                throw new IOException("The artifact changed during transfer. Request its current version again.");
            }
            var line = output.Split('\n').LastOrDefault(line => line.StartsWith(marker + ":", StringComparison.Ordinal));
            var result = line?[(marker.Length + 1)..].TrimEnd('\r');
            if (string.IsNullOrEmpty(result) || !result.StartsWith('/')) throw new IOException("The target did not confirm the artifact path. Retry the transfer.");
            return result;
        }
        finally { gate.Release(); }
    }
    public async Task DeleteAsync(Guid? id, CancellationToken token)
    {
        var gate = Gates.GetOrAdd(store.DirectoryPath, _ => new(1, 1));
        await gate.WaitAsync(token);
        try { await DeleteCoreAsync(id, token); }
        finally { gate.Release(); }
    }
    private async Task DeleteCoreAsync(Guid? id, CancellationToken token)
    {
        if (target.IsLocal || !File.Exists(Marker)) return;
        if (id is { } candidate && !(await store.RemoteIdsAsync(token)).Contains(candidate)) return;
        var command = Setup;
        if (id is { } artifactId) command += "item=\"$owner/" + artifactId.ToString("N") + "\"; test ! -L \"$item\"; rm -rf -- \"$item\";";
        else command += "rm -rf -- \"$owner\";";
        await runCommand(target, command, null, token);
        if (id is { } deletedId) await store.ForgetRemoteAsync(deletedId, token);
    }
}
