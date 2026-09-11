using PiAgentGui.Models.SourceControl;

namespace PiAgentGui.Utilities;

public static class GitBranchParser
{
    public static IReadOnlyList<GitBranch> Parse(string text, string current, IReadOnlyList<string> remotes)
    {
        var result = new List<GitBranch>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\0');
            if (fields.Length != 4) throw new InvalidDataException("Git returned invalid branch information.");
            if (fields[1].Length > 0) continue; // Remote HEAD aliases aren't branches.
            var reference = fields[0];
            if (reference.StartsWith("refs/heads/", StringComparison.Ordinal))
                result.Add(new(reference, reference[11..], false, fields[2], fields[3], reference == current));
            else if (reference.StartsWith("refs/remotes/", StringComparison.Ordinal))
            {
                var name = reference[13..];
                var remote = remotes.OrderByDescending(value => value.Length).FirstOrDefault(value => name.StartsWith(value + "/", StringComparison.Ordinal));
                result.Add(new(reference, name, true, remote, remote is null ? null : "refs/heads/" + name[(remote.Length + 1)..], false));
            }
        }
        if (current.StartsWith("refs/heads/", StringComparison.Ordinal) && result.All(branch => branch.Ref != current))
            result.Insert(0, new(current, current[11..], false, null, null, true));
        return result.OrderByDescending(branch => branch.Current).ThenBy(branch => branch.Remote).ThenBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
