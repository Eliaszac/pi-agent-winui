using System.Runtime.InteropServices;

namespace PiAgentGui.Services.Windowing;

/// <summary>Queues the Windows information sound without blocking the UI.</summary>
public static class CompletionSound
{
    public static void Play() => MessageBeep(0x40);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint type);
}
