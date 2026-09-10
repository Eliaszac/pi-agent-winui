using PiAgentGui.Models.Applications;

namespace PiAgentGui.Utilities;

public static class OpenInSelection
{
    public static InstalledApplication? Resolve(IReadOnlyList<InstalledApplication> applications, string? preferred, string? associated)
    {
        var editors = applications.Where(app => app.Kind is ApplicationKind.Editor or ApplicationKind.SolutionEditor).ToArray();
        return editors.FirstOrDefault(app => app.Id == preferred)
            ?? editors.FirstOrDefault(app => associated is not null &&
                (Path.GetFileName(app.ExecutablePath).Equals(Path.GetFileName(associated), StringComparison.OrdinalIgnoreCase)
                 || app.Id == "visualstudio" && Path.GetFileName(associated).Equals("VSLauncher.exe", StringComparison.OrdinalIgnoreCase)))
            ?? editors.FirstOrDefault() ?? applications.FirstOrDefault(app => app.Kind == ApplicationKind.Explorer);
    }
}
