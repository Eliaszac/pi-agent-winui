using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Reads Windows processes without changing their lifetime or requesting agent work.</summary>
public sealed class AgentProcessReader
{
    public IReadOnlyList<AgentProcess> Read(ProcessIdentity root, IReadOnlySet<ProcessIdentity> known)
    {
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var entries = new List<AgentProcess>();
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" };
            if (!Process32FirstW(snapshot, ref entry)) throw new Win32Exception(Marshal.GetLastWin32Error());
            do
            {
                try
                {
                    using var process = Process.GetProcessById((int)entry.Id);
                    entries.Add(new(new((int)entry.Id, process.StartTime.ToUniversalTime()), (int)entry.ParentId, entry.Name));
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // Protected and already exited processes cannot be inspected.
                }
            } while (Process32NextW(snapshot, ref entry));
            var error = Marshal.GetLastWin32Error();
            if (error != 18) throw new Win32Exception(error);
            if (!entries.Any(item => item.Identity == root)) return [];
            return AgentProcessTree.Select(root, entries, known);
        }
        finally { CloseHandle(snapshot); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Id;
        public UIntPtr Heap;
        public uint ModuleId, Threads, ParentId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint id);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
