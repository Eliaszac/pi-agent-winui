using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Highlights each file state independently, retaining multiline context before hiding unchanged rows.</summary>
public static class DiffSyntaxHighlighter
{
    public static UnifiedDiffDocument Highlight(UnifiedDiffDocument document, string fileName, CancellationToken token = default)
    {
        var rows = document.Lines.ToArray();
        // Pass an extension rather than a full path, which may contain spaces.
        var extension = Path.GetExtension(fileName);
        if (extension.Length == 0) return document;
        var label = "file" + extension;
        var start = 0;
        for (var index = 0; index <= rows.Length; index++)
        {
            if (index < rows.Length && rows[index].Kind is not ('h' or 'n')) continue;
            HighlightSide(rows, start, index, label, true, token);
            HighlightSide(rows, start, index, label, false, token);
            start = index + 1;
        }
        return document with { Lines = rows };
    }

    private static void HighlightSide(DiffLine[] rows, int start, int end, string label, bool old, CancellationToken token)
    {
        var indices = Enumerable.Range(start, end - start).Where(index => old ? rows[index].OldLine is not null : rows[index].NewLine is not null).ToArray();
        if (indices.Length == 0) return;
        token.ThrowIfCancellationRequested();
        var code = string.Concat(indices.Select(index => rows[index].Text + "\n"));
        IReadOnlyList<CodeToken> tokens;
        try { tokens = new CodeSyntaxHighlighter().Highlight(label, code, token); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return; }
        var tokenIndex = 0; var tokenOffset = 0;
        foreach (var index in indices)
        {
            token.ThrowIfCancellationRequested();
            var remaining = rows[index].Text.Length; var spans = new List<CodeToken>();
            while (remaining > 0 && tokenIndex < tokens.Count)
            {
                var part = tokens[tokenIndex];
                var count = Math.Min(remaining, part.Text.Length - tokenOffset);
                if (count > 0) spans.Add(new(part.Text.Substring(tokenOffset, count), part.Kind));
                remaining -= count; tokenOffset += count;
                if (tokenOffset == part.Text.Length) { tokenIndex++; tokenOffset = 0; }
            }
            // Consume the synthetic separator, never rendering it inside a code row.
            while (tokenIndex < tokens.Count && tokenOffset == tokens[tokenIndex].Text.Length) { tokenIndex++; tokenOffset = 0; }
            if (tokenIndex < tokens.Count) { tokenOffset++; if (tokenOffset == tokens[tokenIndex].Text.Length) { tokenIndex++; tokenOffset = 0; } }
            if (remaining == 0)
                rows[index] = old ? rows[index] with { OldTokens = spans } : rows[index] with { NewTokens = spans };
        }
    }
}
