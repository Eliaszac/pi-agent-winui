using System.Diagnostics;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>Builds process arguments without invoking a command shell.</summary>
public sealed class PiProcessStartInfoFactory(PiInstallationLocator locator)
{
    public PiProcessStartInfoFactory(PiRuntimeOptions options) : this(new PiInstallationLocator(options)) { }

    public ProcessStartInfo Create(PiLaunchRequest request)
    {
        if (!Directory.Exists(request.WorkingDirectory)) throw new DirectoryNotFoundException("The project folder is unavailable.");
        var installation = locator.Resolve();
        var info = new ProcessStartInfo
        {
            FileName = installation.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new System.Text.UTF8Encoding(false),
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        if (installation.CliPath is not null) info.ArgumentList.Add(installation.CliPath);
        info.ArgumentList.Add("--mode");
        info.ArgumentList.Add("rpc");
        if (request.ManageProviders)
        {
            var extension = Path.Combine(AppContext.BaseDirectory, "PiExtensions", "providers.ts");
            if (!File.Exists(extension)) throw new FileNotFoundException("The bundled Providers integration is missing. Rebuild or reinstall Pi Agent.");
            foreach (var flag in new[] { "--no-session", "--no-tools", "--no-extensions", "--no-skills", "--no-prompt-templates", "--no-context-files" }) info.ArgumentList.Add(flag);
            info.ArgumentList.Add("--extension");
            info.ArgumentList.Add(extension);
            return info;
        }
        if (!string.IsNullOrWhiteSpace(request.SessionName))
        {
            info.ArgumentList.Add("--name");
            info.ArgumentList.Add(request.SessionName);
        }
        var writeDiffExtension = Path.Combine(AppContext.BaseDirectory, "PiExtensions", "write-diff.ts");
        if (!File.Exists(writeDiffExtension)) throw new FileNotFoundException("The bundled write-diff extension is missing. Rebuild or reinstall Pi Agent.", writeDiffExtension);
        info.ArgumentList.Add("--extension");
        info.ArgumentList.Add(writeDiffExtension);
        info.ArgumentList.Add("--session");
        info.ArgumentList.Add(request.SessionFile);
        info.ArgumentList.Add("--session-dir");
        info.ArgumentList.Add(Path.GetDirectoryName(request.SessionFile)!);
        return info;
    }
}
