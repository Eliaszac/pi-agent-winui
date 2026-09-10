namespace PiAgentGui.Utilities;

/// <summary>Builds render-cache keys without assuming parser spans fit an incomplete streamed source.</summary>
internal static class MarkdownBlockSignature
{
    public static string Create(string blockType, string source, int start, int end)
    {
        // Some parser blocks include an implicit end-of-line beyond the current input.
        // Fall back to the full snapshot so malformed spans cannot crash or conceal later edits.
        var text = start >= 0 && end >= start && end < source.Length
            ? source.Substring(start, end - start + 1)
            : source;
        return blockType + ":" + text;
    }
}
