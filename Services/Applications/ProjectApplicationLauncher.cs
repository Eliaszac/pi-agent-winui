using System.Diagnostics;
using PiAgentGui.Models.Applications;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Applications;

public sealed class ProjectApplicationLauncher
{
    public void OpenFile(InstalledApplication application, string path)
    {
        if (!File.Exists(path) || !File.Exists(application.ExecutablePath)) throw new IOException("The file or editor no longer exists.");
        var start = new ProcessStartInfo(application.ExecutablePath) { WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = false };
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new IOException("The editor did not start.");
    }
    public void Open(InstalledApplication application, string directory)
    {
        if (!Directory.Exists(directory) || !File.Exists(application.ExecutablePath)) throw new IOException("Application or project no longer exists.");
        using var process = Process.Start(CreateStartInfo(application, directory)) ?? throw new IOException("Application did not start.");
    }

    public static ProcessStartInfo CreateStartInfo(InstalledApplication application, string directory)
    {
        if (!Path.IsPathFullyQualified(directory) || !Path.IsPathFullyQualified(application.ExecutablePath)) throw new ArgumentException("Absolute paths are required.");
        var start = new ProcessStartInfo(application.ExecutablePath) { WorkingDirectory = directory, UseShellExecute = false };
        if (application.Kind == ApplicationKind.Terminal)
        {
            start.ArgumentList.Add("-d");
            start.ArgumentList.Add(".");
        }
        else start.ArgumentList.Add(application.Kind == ApplicationKind.SolutionEditor ? ProjectOpenTarget.FindSolution(directory) ?? directory : directory);
        return start;
    }
}
