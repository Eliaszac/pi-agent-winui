namespace PiAgentGui.Services.Files;

/// <summary>File operations bounded to a project, without following directory links.</summary>
public sealed class ProjectFileSystem(string root, Action<string, bool> recycle)
{
    public string Root { get; } = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public bool HasAtMostTenItems(string directory)
    {
        Validate(directory, allowRoot: true);
        return new DirectoryInfo(directory).EnumerateFileSystemInfos().Take(11).Count() <= 10;
    }

    public IReadOnlyList<FileSystemInfo> List(string directory)
    {
        Validate(directory, allowRoot: true);
        var entries = new DirectoryInfo(directory).EnumerateFileSystemInfos().Take(20001).ToArray();
        if (entries.Length > 20000) throw new IOException("This folder has more than 20,000 entries and cannot be displayed in this explorer.");
        return entries
            .OrderByDescending(item => (item.Attributes & FileAttributes.Directory) != 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public string Create(string parent, string name, bool folder)
    {
        using var activity = Utilities.WorkspaceActivityLease.Acquire(false);
        Validate(parent, allowRoot: true);
        var target = Child(parent, name);
        if (Path.Exists(target)) throw new IOException("An item with that name already exists.");
        if (folder) Directory.CreateDirectory(target);
        else using (new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        return target;
    }

    public string Rename(string path, string name) => MoveTo(path, Child(Path.GetDirectoryName(path)!, name));
    public string Move(string path, string parent)
    {
        Validate(parent, allowRoot: true);
        if (!Directory.Exists(parent)) throw new IOException("Drop onto a folder to move an item.");
        return MoveTo(path, Child(parent, Path.GetFileName(path)));
    }

    private string MoveTo(string path, string target)
    {
        using var activity = Utilities.WorkspaceActivityLease.Acquire(false);
        Validate(path);
        Validate(Path.GetDirectoryName(target)!, allowRoot: true);
        if (string.Equals(path, target, StringComparison.Ordinal)) return path;
        if (Directory.Exists(path) && target.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("A folder cannot be moved inside itself.");
        if (Path.Exists(target) && !string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
            throw new IOException("An item with that name already exists. Nothing was overwritten.");
        if (Directory.Exists(path)) Directory.Move(path, target);
        else File.Move(path, target, overwrite: false);
        return target;
    }

    public void Delete(string path)
    {
        using var activity = Utilities.WorkspaceActivityLease.Acquire(false);
        Validate(path);
        recycle(path, Directory.Exists(path));
    }

    public void Validate(string path, bool allowRoot = false)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var prefix = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !(allowRoot && string.Equals(full, Root, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("This operation must stay inside the project folder.");
        for (var current = full; current.Length >= Root.Length; current = Path.GetDirectoryName(current) ?? "")
        {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked folders and files are not modified or traversed by this explorer.");
            if (string.Equals(current, Root, StringComparison.OrdinalIgnoreCase)) break;
        }
    }

    private static string Child(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.EndsWith('.') || name.EndsWith(' ')) throw new IOException("Enter a valid file or folder name without path separators.");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || System.Text.RegularExpressions.Regex.IsMatch(stem, "^(COM|LPT)[1-9¹²³]$"))
            throw new IOException("That name is reserved by Windows.");
        return Path.Combine(parent, name);
    }
}
