using PiAgentGui.Models.Projects;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Files;

namespace PiAgentGui.Services.Files;

/// <summary>Bounded target-native file inspection. Linked entries are displayed but never traversed.</summary>
public sealed class TargetFileReader(ExecutionTarget target, TargetCommandRunner runner)
{
    public string Root => target.Path;
    public async Task<IReadOnlyList<ExplorerItem>> ListAsync(string directory, int depth, CancellationToken token)
    {
        Validate(directory);
        var result = await runner.RunAsync(target, LinkGuard(directory) + $"test -d {PosixShell.Quote(directory)} && find {PosixShell.Quote(directory)} -mindepth 1 -maxdepth 1 -printf '%f\\0%y\\0'", token);
        if (result.ExitCode != 0) throw new IOException("Couldn't read the target folder. Check its connection and permissions.");
        var parts = result.Output.Split('\0');
        if (parts.Length > 40001) throw new IOException("This folder exceeds 20,000 entries.");
        var rows = new List<ExplorerItem>();
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            if (parts[i].Contains('/') || parts[i] is "." or "..") throw new IOException("Invalid target directory entry.");
            rows.Add(new(directory.TrimEnd('/') + "/" + parts[i], parts[i + 1] == "d", depth, false, parts[i + 1] == "l"));
        }
        return rows.OrderByDescending(row => row.IsDirectory).ThenBy(row => row.Name, StringComparer.Ordinal).ToArray();
    }

    public async Task<string> ReadAsync(string file, CancellationToken token)
    {
        Validate(file);
        var result = await runner.RunAsync(target, LinkGuard(file) + $"test -f {PosixShell.Quote(file)} && head -c 262145 -- {PosixShell.Quote(file)}", token);
        if (result.ExitCode != 0) throw new IOException("Couldn't read the target file.");
        if (result.Output.Length > 262144 || result.Output.Contains('\0')) throw new IOException("This file is binary or exceeds the text preview limit.");
        return result.Output;
    }

    public async Task<IReadOnlyList<FileReference>> FindAsync(string query, CancellationToken token)
    {
        var result = await runner.RunAsync(target, "cd -- " + PosixShell.Quote(Root) + " && rg --files --hidden -0 -g '!.git' -g '!node_modules'", token);
        if (result.ExitCode > 1) throw new IOException("Target file search requires ripgrep (rg).");
        return result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(file => file.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(30).Select(file => new FileReference(Root + "/" + file, file)).ToArray();
    }

    private void Validate(string path)
    {
        if (path != Root && !path.StartsWith(Root.TrimEnd('/') + "/", StringComparison.Ordinal) || path.Split('/').Any(part => part is "." or "..") || path.Contains('\0'))
            throw new IOException("The file must stay inside the selected target's workspace.");
    }

    private string LinkGuard(string path)
    {
        var checks = new List<string>();
        for (var current = path; ; current = current[..current.LastIndexOf('/')])
        {
            checks.Add("test ! -L " + PosixShell.Quote(current));
            if (current == Root) break;
        }
        return string.Join(" && ", checks) + " && ";
    }
}
