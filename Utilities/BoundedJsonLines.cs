using System.Text;

namespace PiAgentGui.Utilities;

public static class BoundedJsonLines
{
    /// <summary>A null row marks an oversized line. Call from a background worker.</summary>
    public static IEnumerable<string?> Read(TextReader reader, CancellationToken cancellationToken, int maximumLineLength = 2_000_000)
    {
        var buffer = new char[16384];
        var line = new StringBuilder();
        var oversized = false;
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < count; index++)
            {
                var character = buffer[index];
                if (character == '\n')
                {
                    yield return oversized ? null : line.ToString();
                    line.Clear(); oversized = false;
                }
                else if (!oversized)
                {
                    if (line.Length == maximumLineLength) { line.Clear(); oversized = true; }
                    else line.Append(character);
                }
            }
        }
        if (oversized || line.Length > 0) yield return oversized ? null : line.ToString();
    }
}
