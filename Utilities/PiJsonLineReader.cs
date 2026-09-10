using System.Runtime.CompilerServices;
using System.Text;

namespace PiAgentGui.Utilities;

/// <summary>Reads Pi JSONL records using only LF as a delimiter.</summary>
public static class PiJsonLineReader
{
    public static async IAsyncEnumerable<string> ReadAsync(TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, int maxRecordLength = 16 * 1024 * 1024)
    {
        var buffer = new char[4096];
        var pending = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] == '\n')
                {
                    if (pending.Length > 0 && pending[^1] == '\r') pending.Length--;
                    if (pending.Length > 0) yield return pending.ToString();
                    pending.Clear();
                }
                else
                {
                    pending.Append(buffer[i]);
                    if (pending.Length > maxRecordLength) throw new InvalidDataException("Pi sent an oversized protocol record.");
                }
            }
        }
        if (pending.Length > 0) throw new InvalidDataException("Pi exited with an incomplete protocol record.");
    }
}
