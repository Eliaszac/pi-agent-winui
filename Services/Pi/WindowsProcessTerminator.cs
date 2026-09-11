using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>Terminates only the process whose creation time matches the requested identity.</summary>
public static class WindowsProcessTerminator
{
    public static void Stop(ProcessIdentity identity)
    {
        using var handle = OpenProcess(0x1001, false, identity.Id);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 87) return; // Already exited.
            throw new Win32Exception(error);
        }
        if (!GetProcessTimes(handle, out var created, out _, out _, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (DateTime.FromFileTimeUtc(created) != identity.StartedUtc) return;
        if (!TerminateProcess(handle, 1))
        {
            var error = Marshal.GetLastWin32Error();
            if (GetExitCodeProcess(handle, out var exitCode) && exitCode != 259) return;
            throw new Win32Exception(error);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle handle, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle handle, out uint exitCode);
}
