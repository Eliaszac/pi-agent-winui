namespace PiAgentGui;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // OpenSSH's helper path must never initialize WinUI or display an application window.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PI_DESKTOP_SSH_CREDENTIAL")))
            return Services.Projects.SshAskpass.Run(args);
        StartDesktop();
        return 0;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void StartDesktop()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }
}
