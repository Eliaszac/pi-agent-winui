using System.Diagnostics;
using Microsoft.Win32;

namespace PiAgentGui.Services.Updates;

public sealed class UpdateInstaller
{
    public string? BlockReason()
    {
        try { return ReadBlockReason(); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { return "Could not verify the Windows installation. Close other app windows and try again."; }
    }

    private static string? ReadBlockReason()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PiAgentGui.Desktop_is1");
        var location = key?.GetValue("InstallLocation") as string;
        if (string.IsNullOrEmpty(location) || !Path.GetFullPath(location).TrimEnd('\\').Equals(AppContext.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return "Install Pi desktop with its Windows installer to use Update and restart.";
        foreach (var process in Process.GetProcessesByName("PiAgentGui"))
        {
            using (process)
                if (process.Id != Environment.ProcessId) return "Close other Pi desktop windows before updating.";
        }
        return null;
    }

    public void Launch(string path)
    {
        if (BlockReason() is { } reason) throw new InvalidOperationException(reason);
        var start = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)!, CreateNoWindow = true };
        foreach (var argument in new[] { "/SILENT", "/NORESTART", "/NOCLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/PIUPDATE=1", $"/PIPARENT={Environment.ProcessId}" }) start.ArgumentList.Add(argument);
        using var installer = Process.Start(start) ?? throw new IOException("The update installer could not start.");
    }
}
