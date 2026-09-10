using System.Text;

namespace PiAgentGui.Services.Terminal;

/// <summary>Forwards available pipe bytes without waiting for a line or a full character buffer.</summary>
internal static class TerminalOutputReader
{
    public static void Read(Stream stream, Action<string> output)
    {
        var encoding = new UTF8Encoding(false);
        var decoder = encoding.GetDecoder();
        var bytes = new byte[8192];
        var characters = new char[encoding.GetMaxCharCount(bytes.Length)];
        int count;
        do
        {
            count = stream.Read(bytes, 0, bytes.Length);
            var decoded = decoder.GetChars(bytes, 0, count, characters, 0, flush: count == 0);
            if (decoded > 0) output(new string(characters, 0, decoded));
        } while (count > 0);
    }
}
