using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

public static class DiffContextFilter
{
    public static IReadOnlyList<DiffLine> ChangesOnly(IReadOnlyList<DiffLine> lines, int context = 3, IReadOnlySet<int>? revealed = null)
    {
        context = Math.Clamp(context, 0, 20);
        var keep = new bool[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            if (!lines[index].IsAdded && !lines[index].IsRemoved) continue;
            keep[index] = true;
            for (var step = 1; step <= context && index - step >= 0; step++)
            { if (lines[index - step].Kind != ' ') break; keep[index - step] = true; }
            for (var step = 1; step <= context && index + step < lines.Count; step++)
            { if (lines[index + step].Kind != ' ') break; keep[index + step] = true; }
        }
        var rows = new List<DiffLine>();
        var skipped = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Kind == ' ' && !keep[index] && revealed?.Contains(index) != true) { skipped++; continue; }
            if (skipped > 0) { rows.Add(Gap(index - skipped, skipped)); skipped = 0; }
            rows.Add(line);
        }
        if (skipped > 0) rows.Add(Gap(lines.Count - skipped, skipped));
        return rows;
    }

    public static void RevealNext(DiffLine gap, ISet<int> revealed, int count = 15)
    {
        if (!gap.CanExpand) return;
        for (var offset = 0; offset < Math.Min(gap.HiddenCount, Math.Clamp(count, 1, 20)); offset++)
            revealed.Add(gap.HiddenStart + offset);
    }

    private static DiffLine Gap(int start, int count) => new($"⋯ {count} unchanged lines", null, null, 'h')
    { HiddenStart = start, HiddenCount = count };
}
