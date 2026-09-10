using PiAgentGui.Configuration;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>Checks installation files without launching Pi, shared by startup and process creation.</summary>
public sealed class PiInstallationLocator(PiRuntimeOptions options, IReadOnlyList<string>? searchDirectories = null)
{
    public PiInstallation Resolve()
    {
        var directories = searchDirectories ?? (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(path => path.Trim().Trim('"')).Where(Path.IsPathFullyQualified)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var executable = options.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            executable = directories.Select(directory => Path.Combine(directory, "pi.exe")).FirstOrDefault(File.Exists);
            executable ??= directories.SelectMany(directory => new[]
            {
                Path.Combine(directory, "node_modules", "@earendil-works", "pi-coding-agent", "dist", "cli.js"),
                Path.Combine(directory, "node_modules", "@mariozechner", "pi-coding-agent", "dist", "cli.js")
            }).FirstOrDefault(File.Exists);
        }
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new FileNotFoundException("Pi couldn't be found on this computer. Follow the installation instructions on the Pi website.");
        if (Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return new(executable);
        if (!Path.GetExtension(executable).Equals(".js", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The configured Pi location must point to pi.exe or dist/cli.js, not a shell script.");
        var node = options.NodePath;
        if (string.IsNullOrWhiteSpace(node)) node = directories.Select(directory => Path.Combine(directory, "node.exe")).FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(node) || !Path.IsPathFullyQualified(node) || !File.Exists(node))
            throw new FileNotFoundException("Pi was found, but its Node.js runtime is missing. Complete the setup instructions on the Pi website.");
        return new(node, executable);
    }
}
