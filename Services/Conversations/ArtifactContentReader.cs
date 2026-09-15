using System.Text;
using System.Text.Json.Nodes;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

public sealed class ArtifactContentReader
{
    public async Task<JsonObject> ReadAsync(string path, int offset, int limit, CancellationToken token)
    {
        if (offset < 0 || offset > ArtifactStore.MaximumBytes || limit is < 1 or > 16000) throw new IOException("Use a character offset of 0 or more and a limit from 1 to 16000.");
        await using var stream = File.OpenRead(path);
        var signature = new byte[12];
        var length = await stream.ReadAsync(signature, token);
        stream.Position = 0;
        var mime = ArtifactFileTypes.ImageMime(signature.AsSpan(0, length));
        if (mime is not null)
        {
            if (stream.Length > PiImageContent.MaximumImageBytes) throw new IOException("This image exceeds the 2 MB model-image limit. Use artifact_path and resize a working copy on the target.");
            var bytes = await ArtifactSourceReader.ReadBoundedAsync(stream, token, PiImageContent.MaximumImageBytes);
            return new() { ["image"] = Convert.ToBase64String(bytes), ["mimeType"] = mime };
        }
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, 4096, leaveOpen: true);
        var buffer = new char[Math.Min(4096, Math.Max(offset, limit))];
        try
        {
            var skipped = 0;
            while (skipped < offset)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, offset - skipped)), token);
                if (read == 0) break;
                skipped += read;
            }
            var output = new char[limit + 2];
            var count = await reader.ReadBlockAsync(output.AsMemory(), token);
            if (output.AsSpan(0, count).Contains('\0')) throw new IOException("This file is binary. Use artifact_path and the appropriate file-reading tool on the target.");
            var returned = Math.Min(count, limit);
            if (returned > 0 && returned < count && char.IsHighSurrogate(output[returned - 1]) && char.IsLowSurrogate(output[returned])) returned++;
            return new() { ["text"] = new string(output, 0, returned), ["offset"] = skipped, ["nextOffset"] = skipped + returned, ["hasMore"] = count > returned };
        }
        catch (DecoderFallbackException) { throw new IOException("This file is not supported Unicode text. Use artifact_path to read it with a suitable tool on the target."); }
    }
}
