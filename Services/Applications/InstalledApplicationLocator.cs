using Microsoft.Win32;
using System.Text.Json;
using PiAgentGui.Models.Applications;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Applications;

/// <summary>Read-only bounded discovery; never executes installers or arbitrary registered commands.</summary>
public sealed class InstalledApplicationLocator : IApplicationLocator
{
    public IReadOnlyList<InstalledApplication> Discover()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Path.Combine(local, "Programs") };
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(path => path.Trim().Trim('"')).Where(Path.IsPathFullyQualified).ToArray();
        var installs = ReadInstallLocations();
        var result = new List<InstalledApplication>();
        foreach (var definition in ApplicationCatalog.Definitions)
        {
            var candidates = new List<string>();
            candidates.AddRange(ReadAppPaths(definition.Executable));
            foreach (var path in paths)
            {
                candidates.Add(Path.Combine(path, definition.Executable));
                // Electron launchers usually live in bin, beside the app directory.
                if (definition.Id is "vscode" or "cursor" or "windsurf") candidates.Add(Path.GetFullPath(Path.Combine(path, "..", definition.Executable)));
            }
            foreach (var install in installs.Where(item => item.Name.Contains(definition.Name, StringComparison.OrdinalIgnoreCase) ||
                (definition.Folder is not null && item.Name.Contains(definition.Folder, StringComparison.OrdinalIgnoreCase))))
                AddInstallCandidates(candidates, install.Path, definition.Executable);
            if (definition.Folder is not null)
            {
                foreach (var root in roots)
                {
                    AddInstallCandidates(candidates, Path.Combine(root, definition.Folder), definition.Executable);
                    foreach (var folder in Directories(Path.Combine(root, "JetBrains")).Where(folder => Path.GetFileName(folder).StartsWith(definition.Folder, StringComparison.OrdinalIgnoreCase)))
                        AddInstallCandidates(candidates, folder, definition.Executable);
                }
                foreach (var folder in Directories(Path.Combine(local, "JetBrains", "Toolbox", "apps")))
                {
                    AddProductInfoCandidate(candidates, folder, definition.Executable);
                    // Older Toolbox layouts: product/channel/version. Bounded depth, never scan the drive.
                    foreach (var channel in Directories(folder))
                        foreach (var version in Directories(channel)) AddProductInfoCandidate(candidates, version, definition.Executable);
                }
            }
            if (definition.Id == "terminal") candidates.Add(Path.Combine(local, "Microsoft", "WindowsApps", "wt.exe"));
            if (definition.Id == "visualstudio")
            {
                foreach (var root in roots.Take(2))
                    foreach (var year in Directories(Path.Combine(root, "Microsoft Visual Studio")))
                        foreach (var edition in Directories(year)) candidates.Add(Path.Combine(edition, "Common7", "IDE", "devenv.exe"));
                foreach (var instance in Directories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "VisualStudio", "Packages", "_Instances")))
                {
                    try
                    {
                        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(instance, "state.json")));
                        if (state.RootElement.TryGetProperty("installationPath", out var path) && path.GetString() is { } install)
                            candidates.Add(Path.Combine(install, "Common7", "IDE", "devenv.exe"));
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException) { }
                }
            }
            var executable = candidates.FirstOrDefault(path => Path.IsPathFullyQualified(path) && File.Exists(path));
            if (executable is not null) result.Add(new(definition.Id, definition.Name, executable, definition.Logo, definition.Kind));
        }
        result.Add(new("explorer", "File Explorer", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "explorer.svg", ApplicationKind.Explorer));
        return result;
    }

    public string? GetAssociatedExecutable(string directory) => ProjectOpenTarget.FindSolution(directory) is { } solution
        ? WindowsFileAssociation.GetExecutable(Path.GetExtension(solution)) : null;

    private static void AddInstallCandidates(List<string> paths, string directory, string executable)
    {
        paths.Add(Path.Combine(directory, executable));
        paths.Add(Path.Combine(directory, "bin", executable));
        AddProductInfoCandidate(paths, directory, executable);
    }

    private static void AddProductInfoCandidate(List<string> paths, string directory, string executable)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "product-info.json")));
            foreach (var launch in document.RootElement.GetProperty("launch").EnumerateArray())
            {
                if (launch.TryGetProperty("launcherPath", out var field) && field.GetString() is { } relative &&
                    Path.GetFileName(relative).Equals(executable, StringComparison.OrdinalIgnoreCase))
                {
                    var full = Path.GetFullPath(Path.Combine(directory, relative));
                    if (full.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) paths.Add(full);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException) { }
    }

    private static string[] Directories(string root)
    {
        try { return Directory.GetDirectories(root).OrderDescending(StringComparer.OrdinalIgnoreCase).Take(100).ToArray(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return []; }
    }

    private static List<(string Name, string Path)> ReadInstallLocations()
    {
        var result = new List<(string, string)>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                    foreach (var name in uninstall?.GetSubKeyNames() ?? [])
                    {
                        using var item = uninstall!.OpenSubKey(name);
                        if (item?.GetValue("DisplayName") is string display && item.GetValue("InstallLocation") is string path && Path.IsPathFullyQualified(path.Trim('"')))
                            result.Add((display, path.Trim('"')));
                    }
                }
                catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return result;
    }

    private static IEnumerable<string> ReadAppPaths(string executable)
    {
        var result = new List<string>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + executable);
                    if (key?.GetValue(null) is string path) result.Add(Environment.ExpandEnvironmentVariables(path.Trim('"')));
                }
                catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return result;
    }
}
