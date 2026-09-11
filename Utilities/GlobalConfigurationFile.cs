using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PiAgentGui.Utilities;

/// <summary>Bounded, serialized updates that preserve unrelated Pi configuration fields.</summary>
public sealed class GlobalConfigurationFile(string path)
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    public async Task<JsonObject> ReadAsync(CancellationToken token = default) => Parse(await ReadBytesAsync(token));
    public async Task UpdateAsync(Action<JsonObject> update, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var lease = await AcquireAsync(token);
        var original = await ReadBytesAsync(token);
        var document = Parse(original);
        update(document);
        var bytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        if (bytes.Length > MaximumBytes) throw new IOException("Configuration exceeds the 2 MB limit.");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token);
            var current = await ReadBytesAsync(token);
            if (!SHA256.HashData(original).SequenceEqual(SHA256.HashData(current)))
                throw new IOException("Configuration changed outside this window. Preview again before saving.");
            token.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task<byte[]> ReadBytesAsync(CancellationToken token)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, true);
            if (stream.Length > MaximumBytes) throw new IOException("Configuration exceeds the 2 MB limit.");
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int length;
            while ((length = await stream.ReadAsync(buffer, token)) > 0)
            {
                if (memory.Length + length > MaximumBytes) throw new IOException("Configuration exceeds the 2 MB limit.");
                memory.Write(buffer, 0, length);
            }
            return memory.ToArray();
        }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
    }

    private static JsonObject Parse(byte[] bytes)
    {
        if (bytes.Length == 0) return new();
        var text = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new IOException("Existing configuration must be a JSON object.");
    }

    private async Task<FileStream> AcquireAsync(CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(path + ".pi-gui.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33 && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            { await Task.Delay(50, token); }
        }
    }
}
