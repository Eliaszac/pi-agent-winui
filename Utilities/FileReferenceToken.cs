namespace PiAgentGui.Utilities;

public sealed record FileReferenceToken(int Start, int Length, string Query, string? Kind = null)
{
    public static FileReferenceToken? Find(string text, int caret, int selectionLength = 0)
    {
        if (selectionLength != 0 || caret < 0 || caret > text.Length) return null;
        for (var candidate = caret - 1; candidate >= 0 && text[candidate] is not ('\r' or '\n'); candidate--)
        {
            if (text[candidate] != '@' || (candidate > 0 && !char.IsWhiteSpace(text[candidate - 1]))) continue;
            var typed = text[(candidate + 1)..caret];
            var colon = typed.IndexOf(':');
            var kind = colon < 0 ? null : NormalizeKind(typed[..colon]);
            if (kind is null) break;
            var scopedQuery = typed[(colon + 1)..];
            if (scopedQuery.Contains('"')) return null;
            var scopedEnd = caret;
            while (scopedEnd < text.Length && !char.IsWhiteSpace(text[scopedEnd])) scopedEnd++;
            return new(candidate, scopedEnd - candidate, scopedQuery.TrimStart(), kind);
        }
        var start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        if (start == caret || text[start] != '@') return null;
        var query = text[(start + 1)..caret];
        if (query.Contains('"')) return null;
        var end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        return new(start, end - start, query);
    }

    private static string? NormalizeKind(string value) => value.ToLowerInvariant() switch
    {
        "f" or "file" or "files" => "files",
        "p" or "pr" or "prs" => "prs",
        "i" or "issue" or "issues" => "issues",
        "s" or "script" or "scripts" => "scripts",
        _ => null
    };

    public string Insert(string text, string path) => text.Remove(Start, Length).Insert(Start, Quote(path));
    public static string Quote(string path) => "@\"" + path.Replace('\\', '/') + "\" ";
}
