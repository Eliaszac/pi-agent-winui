using PiAgentGui.Models.SourceControl;

namespace PiAgentGui.Utilities;

/// <summary>Parses NUL-delimited status and numstat with rename detection disabled.</summary>
public static class GitStatusParser
{
    public static IReadOnlyList<GitChange> Parse(string status, string stagedStats, string workingStats)
    {
        var staged = Stats(stagedStats); var working = Stats(workingStats);
        var rows = new List<GitChange>();
        foreach (var record in status.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (record.Length < 4 || record[2] != ' ') throw new InvalidDataException("Git returned an invalid file status.");
            var path = record[3..]; var x = record[0]; var y = record[1];
            if (x == '!' && y == '!') continue;
            if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D'))
            { rows.Add(new(path, 'U', false, null, null)); continue; }
            if (x == '?' && y == '?') { rows.Add(new(path, '?', false, null, null)); continue; }
            if (x != ' ') rows.Add(Change(path, x, true, staged));
            if (y != ' ') rows.Add(Change(path, y, false, working));
        }
        return rows;
    }

    private static GitChange Change(string path, char status, bool staged, Dictionary<string, (long? Added, long? Removed, bool Binary)> stats)
    {
        var value = stats.GetValueOrDefault(path);
        return new(path, status, staged, value.Added, value.Removed, value.Binary);
    }

    private static Dictionary<string, (long?, long?, bool)> Stats(string text)
    {
        var result = new Dictionary<string, (long?, long?, bool)>(StringComparer.Ordinal);
        foreach (var record in text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Split('\t', 3);
            if (fields.Length != 3) throw new InvalidDataException("Git returned invalid line counts.");
            if (fields[0] == "-" && fields[1] == "-") result[fields[2]] = (null, null, true);
            else if (long.TryParse(fields[0], out var added) && long.TryParse(fields[1], out var removed)) result[fields[2]] = (added, removed, false);
            else throw new InvalidDataException("Git returned invalid line counts.");
        }
        return result;
    }
}
