using System.Text.RegularExpressions;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Turns unified patches into numbered code rows without exposing transport markers.</summary>
public static partial class UnifiedDiffParser
{
    public const int MaximumLines = 2000;
    public static UnifiedDiffDocument Parse(string? patch, int maximumLines = MaximumLines)
    {
        maximumLines = Math.Clamp(maximumLines, 1, 20000);
        var rows = new List<DiffLine>();
        var file = ""; var previousFile = ""; var notice = "";
        long oldLine = 0, newLine = 0, oldRemaining = 0, newRemaining = 0;
        var hunks = 0;
        using var reader = new StringReader(patch ?? "");
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (rows.Count >= maximumLines) { notice = $"Preview limited to {maximumLines.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} lines. Copy diff includes the complete patch."; break; }
            var match = HunkHeader().Match(line);
            if (match.Success)
            {
                if (!long.TryParse(match.Groups[1].Value, out oldLine) || !long.TryParse(match.Groups[3].Value, out newLine)
                    || !TryCount(match.Groups[2].Value, out oldRemaining) || !TryCount(match.Groups[4].Value, out newRemaining)
                    || oldLine > int.MaxValue || newLine > int.MaxValue || oldRemaining > int.MaxValue || newRemaining > int.MaxValue)
                { notice = "Some diff sections could not be displayed."; break; }
                if (hunks++ > 0) rows.Add(new("", null, null, 'h'));
                continue;
            }
            if (line.StartsWith("\\ No newline at end of file", StringComparison.Ordinal))
            {
                var previous = rows.LastOrDefault();
                rows.Add(new("No newline at end of file", null, null, previous?.IsRemoved == true ? 'o' : previous?.IsAdded == true ? 'p' : 'n'));
                continue;
            }
            if (oldRemaining > 0 || newRemaining > 0)
            {
                if (line.Length == 0) { notice = "Some diff sections could not be displayed."; break; }
                var kind = line[0];
                if (kind == '+' && newRemaining > 0) { rows.Add(Row(line[1..], null, newLine++, kind, ref notice)); newRemaining--; }
                else if (kind == '-' && oldRemaining > 0) { rows.Add(Row(line[1..], oldLine++, null, kind, ref notice)); oldRemaining--; }
                else if (kind == ' ' && oldRemaining > 0 && newRemaining > 0)
                { rows.Add(Row(line[1..], oldLine++, newLine++, kind, ref notice)); oldRemaining--; newRemaining--; }
                else { notice = "Some diff sections could not be displayed."; break; }
                continue;
            }
            if (line.StartsWith("--- ", StringComparison.Ordinal)) previousFile = FileLabel(line[4..]);
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                var label = FileLabel(line[4..]);
                if (label == "/dev/null") label = previousFile;
                if (file.Length == 0) file = label;
                else if (rows.Count > 0) rows.Add(new(label == file ? "Next captured edit" : label, null, null, 'n'));
            }
        }
        if (notice.Length == 0 && (oldRemaining > 0 || newRemaining > 0)) notice = "This saved diff is incomplete.";
        if (rows.Count == 0 && notice.Length == 0) notice = "No text diff available.";
        return new(file, rows, notice);
    }

    private static DiffLine Row(string text, long? oldLine, long? newLine, char kind, ref string notice)
    {
        if (text.Length > 8000) { text = text[..8000] + "…"; notice = "Long lines shortened in this preview. Copy diff includes the complete patch."; }
        return new(text, oldLine, newLine, kind);
    }
    private static bool TryCount(string value, out long count)
    {
        count = 1;
        return value.Length == 0 || long.TryParse(value, out count);
    }
    private static string FileLabel(string value)
    {
        var name = value.Split('\t', 2)[0].Trim('"');
        if (name.StartsWith("a/", StringComparison.Ordinal) || name.StartsWith("b/", StringComparison.Ordinal)) name = name[2..];
        return ProjectPathDisplay.ForTool(name);
    }
    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.CultureInvariant)]
    private static partial Regex HunkHeader();
}
