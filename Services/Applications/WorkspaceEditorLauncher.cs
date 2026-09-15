using System.Diagnostics;
using PiAgentGui.Models.Applications;
using PiAgentGui.Models.Projects;

namespace PiAgentGui.Services.Applications;

/// <summary>Opens confirmed files only through discovered editors, never through executable file associations.</summary>
public sealed class WorkspaceEditorLauncher(IApplicationLocator locator, OpenInPreferenceStore preferences)
{
    public Task OpenAsync(ExecutionTarget target, string file, int? line, int? column) => Task.Run(() =>
    {
        var applications = locator.Discover().Where(app => app.Kind is ApplicationKind.Editor or ApplicationKind.SolutionEditor).ToArray();
        var preferred = preferences.Read();
        var editor = preferred is not null ? applications.FirstOrDefault(app => app.Id == preferred)
            : Utilities.OpenInSelection.Resolve(applications, null, target.IsLocal ? locator.GetAssociatedExecutable(target.Path) : null);
        if (editor is null) throw new IOException("No preferred editor is available. Choose an installed editor in Settings.");
        using var process = Process.Start(CreateStartInfo(editor, target, file, line, column)) ?? throw new IOException("The editor did not start.");
    });

    public static ProcessStartInfo CreateStartInfo(InstalledApplication editor, ExecutionTarget target, string file, int? line, int? column)
    {
        var start = new ProcessStartInfo(editor.ExecutablePath) { UseShellExecute = false };
        var code = editor.Id is "vscode" or "cursor" or "windsurf";
        if (!target.IsLocal)
        {
            if (editor.Id != "vscode") throw new IOException("Remote file links currently require VS Code and its WSL or Remote SSH extension. Choose VS Code in Settings, or copy the path.");
            start.ArgumentList.Add("--remote");
            start.ArgumentList.Add((target.Kind == "wsl" ? "wsl+" : "ssh-remote+") + target.Host);
        }
        if (code)
        {
            start.ArgumentList.Add("--goto");
            start.ArgumentList.Add(file + (line is not null ? $":{line}:{column ?? 1}" : ""));
        }
        else if (editor.Id is "rider" or "idea" or "webstorm" or "pycharm" or "clion" or "goland" or "phpstorm" or "rubymine" or "rustrover")
        {
            if (line is not null) { start.ArgumentList.Add("--line"); start.ArgumentList.Add(line.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            if (column is not null) { start.ArgumentList.Add("--column"); start.ArgumentList.Add(column.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            start.ArgumentList.Add(file);
        }
        else if (editor.Id == "zed") start.ArgumentList.Add(file + (line is not null ? $":{line}:{column ?? 1}" : ""));
        else if (editor.Id == "visualstudio")
        {
            start.ArgumentList.Add("/Edit"); start.ArgumentList.Add(file);
            if (line is not null) { start.ArgumentList.Add("/Command"); start.ArgumentList.Add($"Edit.Goto {line}"); }
        }
        else start.ArgumentList.Add(file);
        return start;
    }
}
