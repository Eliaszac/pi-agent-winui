using PiAgentGui.Services.Files;

namespace PiAgentGui.ViewModels.Files;

public static class ExplorerSnapshot
{
    public static void ExpandSmallRootChildren(ProjectFileSystem files, HashSet<string> expanded)
    {
        foreach (var item in files.List(files.Root))
        {
            if ((item.Attributes & FileAttributes.Directory) == 0 || (item.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            try
            {
                if (files.HasAtMostTenItems(item.FullName)) expanded.Add(item.FullName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Leave inaccessible or disappearing folders collapsed for explicit opening.
            }
        }
    }

    public static IReadOnlyList<ExplorerItem> Read(ProjectFileSystem files, IReadOnlySet<string> expanded)
    {
        var rows = new List<ExplorerItem>();
        Add(files.Root, true, 0, false, files, expanded, rows);
        return rows;
    }

    public static IReadOnlyList<ExplorerItem> ReadBranch(ProjectFileSystem files, string path, int depth, IReadOnlySet<string> expanded)
    {
        var rows = new List<ExplorerItem>();
        Add(path, true, depth, false, files, expanded, rows);
        return rows.Skip(1).ToArray();
    }

    private static void Add(string path, bool folder, int depth, bool linked, ProjectFileSystem files, IReadOnlySet<string> expanded, List<ExplorerItem> rows)
    {
        if (rows.Count >= 20000) throw new IOException("Too many visible entries. Collapse some folders, then refresh.");
        var opened = folder && !linked && expanded.Contains(path);
        rows.Add(new(path, folder, depth, opened, linked));
        if (!opened) return;
        foreach (var item in files.List(path))
            Add(item.FullName, (item.Attributes & FileAttributes.Directory) != 0, depth + 1,
                (item.Attributes & FileAttributes.ReparsePoint) != 0, files, expanded, rows);
    }
}
