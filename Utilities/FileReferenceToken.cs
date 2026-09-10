namespace PiAgentGui.Utilities;

public sealed record FileReferenceToken(int Start, int Length, string Query)
{
    public static FileReferenceToken? Find(string text, int caret, int selectionLength = 0)
    {
        if (selectionLength != 0 || caret < 0 || caret > text.Length) return null;
        var start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        if (start == caret || text[start] != '@') return null;
        var query = text[(start + 1)..caret];
        if (query.Contains('"')) return null;
        var end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        return new(start, end - start, query);
    }

    public string Insert(string text, string path) => text.Remove(Start, Length).Insert(Start, Quote(path));
    public static string Quote(string path) => "@\"" + path.Replace('\\', '/') + "\" ";
}
