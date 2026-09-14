using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>A filename or path candidate in prose, with an optional one-based source location.</summary>
public sealed record FileMention(int Start, int Length, string Text, string Path, int? Line, int? Column);

/// <summary>Recognizes file-shaped tokens without interpreting URLs, email addresses, or arbitrary words as files.</summary>
public static partial class FileMentionParser
{
    [GeneratedRegex("(?<![\\w@./\\\\:-])(?<path>(?:[A-Za-z]:[\\\\/]|/|\\./|\\.\\./)?(?:[\\w.@()+-]+[\\\\/])*(?:[\\w@()+-]+(?:\\.[\\w+-]+)+|\\.[A-Za-z][\\w.-]*|Dockerfile|Makefile|README|LICENSE|AGENTS))(?:[:](?<line>[1-9][0-9]*)(?:[:](?<column>[1-9][0-9]*))?|#L(?<line>[1-9][0-9]*))?(?![\\w@/\\\\])", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Candidates();

    public static IReadOnlyList<FileMention> Find(string text)
    {
        // Oversized/pathological prose remains ordinary text rather than blocking rendering.
        if (text.Length > 131072) return [];
        Match[] candidates;
        try { candidates = Candidates().Matches(text).Take(256).ToArray(); }
        catch (RegexMatchTimeoutException) { return []; }
        var result = new List<FileMention>();
        foreach (var match in candidates)
        {
            var path = match.Groups["path"].Value.TrimEnd('.');
            var extension = path[(path.LastIndexOf('.') + 1)..];
            if (!path.Contains('/') && !path.Contains('\\') && (path.Contains('@') || extension is "com" or "org" or "net" or "dev" or "io" or "app" || path.All(c => char.IsDigit(c) || c == '.'))) continue;
            // A match inside an unparsed URL must never become a workspace action.
            var tokenStart = match.Index;
            while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1]) && text[tokenStart - 1] is not '(' and not '<' and not '"') tokenStart--;
            if (text[tokenStart..match.Index].Contains("://", StringComparison.Ordinal) || text[tokenStart..match.Index].Contains('@')) continue;
            int? line = int.TryParse(match.Groups["line"].Value, out var l) ? l : null;
            int? column = int.TryParse(match.Groups["column"].Value, out var c) ? c : null;
            var display = line is null ? match.Value.TrimEnd('.') : match.Value;
            result.Add(new(match.Index, display.Length, display, path, line, column));
        }
        return result;
    }
}
