using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Aligns replacement blocks without pairing changes across context or captured-edit boundaries.</summary>
public static class SplitDiffBuilder
{
    public static SplitDiffRows Build(IReadOnlyList<DiffLine> lines)
    {
        var left = new List<DiffLine>(); var right = new List<DiffLine>();
        var empty = new DiffLine("", null, null, ' ');
        for (var index = 0; index < lines.Count;)
        {
            var line = lines[index];
            if (!line.IsAdded && !line.IsRemoved)
            {
                left.Add(line with { NewLine = null }); right.Add(line with { OldLine = null }); index++; continue;
            }
            var removed = new List<DiffLine>(); var added = new List<DiffLine>();
            DiffLine? oldNotice = null; DiffLine? newNotice = null;
            while (index < lines.Count && (lines[index].IsAdded || lines[index].IsRemoved || lines[index].Kind is 'o' or 'p'))
            {
                var changed = lines[index++];
                if (changed.Kind == 'o') oldNotice = changed;
                else if (changed.Kind == 'p') newNotice = changed;
                else if (changed.IsRemoved) removed.Add(changed); else added.Add(changed);
            }
            for (var row = 0; row < Math.Max(removed.Count, added.Count); row++)
            { left.Add(row < removed.Count ? removed[row] : empty); right.Add(row < added.Count ? added[row] : empty); }
            if (oldNotice is not null || newNotice is not null) { left.Add(oldNotice ?? empty); right.Add(newNotice ?? empty); }
        }
        return new(left, right);
    }
}
