using PiAgentGui.Models.SourceControl;

namespace PiAgentGui.Utilities;

/// <summary>Reads Git merge/diff3/zdiff3 markers without changing surrounding text or line endings.</summary>
public static class ConflictMarkers
{
    public static IReadOnlyList<ConflictBlock> Parse(string text)
    {
        var result = new List<ConflictBlock>();
        int start = -1, left = 0, ancestor = -1, divider = -1, right = 0, width = 0;
        for (var offset = 0; offset < text.Length;)
        {
            var end = text.IndexOfAny(['\r', '\n'], offset);
            var next = end < 0 ? text.Length : end + (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1);
            var line = text.AsSpan(offset, (end < 0 ? text.Length : end) - offset).TrimEnd('\r');
            if (start < 0 && IsMarker(line, '<', out var size))
            { start = offset; left = next; width = size; ancestor = divider = -1; }
            else if (start >= 0 && IsMarker(line, '|', out size) && size == width && divider < 0) ancestor = offset;
            else if (start >= 0 && IsMarker(line, '=', out size) && size == width && divider < 0) { divider = offset; right = next; }
            else if (start >= 0 && IsMarker(line, '>', out size) && size == width && divider >= 0)
            {
                string? basis = null;
                if (ancestor >= 0) { var boundary = text.IndexOfAny(['\r', '\n'], ancestor); var basisStart = boundary + (text[boundary] == '\r' && boundary + 1 < text.Length && text[boundary + 1] == '\n' ? 2 : 1); basis = text[basisStart..divider]; }
                result.Add(new(start, next - start, text[left..(ancestor >= 0 ? ancestor : divider)], text[right..offset], basis));
                start = -1;
            }
            offset = next;
        }
        return result;
    }

    public static bool HasMarkers(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            if (IsMarker(line.AsSpan(), '<', out _) || IsMarker(line.AsSpan(), '>', out _) || IsMarker(line.AsSpan(), '|', out _)) return true;
        return false;
    }

    private static bool IsMarker(ReadOnlySpan<char> line, char marker, out int width)
    {
        width = 0;
        while (width < line.Length && line[width] == marker) width++;
        return width >= 7 && (width == line.Length || line[width] is ' ' or '\t');
    }
}
