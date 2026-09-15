using System.Text;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Separates binary file bytes from WSL/SSH shell startup and logout messages.</summary>
public sealed class ArtifactTransferFrame
{
    private readonly string nonce = Guid.NewGuid().ToString("N");
    public string Header => "\nPI_ARTIFACT_BEGIN:" + nonce + "\n";
    public string Footer => "\nPI_ARTIFACT_END:" + nonce + "\n";
    public const int MaximumOverhead = 128 * 1024;

    public string Command(string path)
    {
        if (!path.StartsWith('/') || path.Contains('\0')) throw new IOException("Use an absolute file path on the selected target.");
        var quoted = PosixShell.Quote(path);
        return $"test -f {quoted} && printf '%s' {PosixShell.Quote(Header)} && head -c {ArtifactStore.MaximumBytes + 1} -- {quoted} && printf '%s' {PosixShell.Quote(Footer)}";
    }

    public byte[] Decode(byte[] response)
    {
        var header = Encoding.ASCII.GetBytes(Header);
        var footer = Encoding.ASCII.GetBytes(Footer);
        var start = response.AsSpan().IndexOf(header);
        var end = response.AsSpan().LastIndexOf(footer);
        if (start < 0 || end < start + header.Length || start > MaximumOverhead / 2 || response.Length - end - footer.Length > MaximumOverhead / 2)
            throw new IOException("The target returned an incomplete artifact transfer. Check its shell configuration and retry.");
        var length = end - start - header.Length;
        if (length > ArtifactStore.MaximumBytes) throw new IOException("Artifacts can be up to 32 MB.");
        return response.AsSpan(start + header.Length, length).ToArray();
    }
}
