namespace PiAgentGui.Utilities;

/// <summary>Finds the slash token at the caret, without treating paths or URLs as commands.</summary>
public sealed record SlashCommandToken(int Start, int Length, string Query)
{
    public static SlashCommandToken? Find(string text, int caret, int selectionLength = 0)
    {
        if (selectionLength != 0 || caret < 0 || caret > text.Length) return null;
        var start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        if (start == caret || text[start] != '/') return null;
        var query = text[(start + 1)..caret];
        if (query.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or ':'))) return null;
        var end = caret;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '-' or '_' or ':')) end++;
        if (end < text.Length && text[end] is '/' or '\\' or '.') return null;
        return new(start, end - start, query);
    }

    public string RemoveFrom(string text) => text.Remove(Start, Length);
}
