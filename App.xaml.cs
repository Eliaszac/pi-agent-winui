using PiAgentGui.Configuration;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.Services.Projects;
using PiAgentGui.ViewModels.Projects;
using PiAgentGui.ViewModels.Shell;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Services.Windowing;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Startup;

namespace PiAgentGui;

/// <summary>Composes application dependencies and owns the main window.</summary>
public partial class App : Application
{
    private Window? window;
    private ConversationWorkspaceStore? workspaces;
    private bool closing;
    private bool canClose;
    private Task? shutdown;
    private bool windowClosed;

    /// <summary>Initializes native application resources and system theming.</summary>
    public App() => InitializeComponent();

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new Window { Title = ApplicationIdentity.Name };
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Pi.ico"));
        window.AppWindow.TitleBar.PreferredTheme = Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        var runtime = PiRuntimeOptions.FromEnvironment();
        var locator = new PiInstallationLocator(runtime);
        var startup = new StartupViewModel(locator);
        window.Content = new StartupPage(startup);
        window.Closed += (_, _) => windowClosed = true;
        window.AppWindow.Closing += OnClosing;
        window.Activate();
        if (!await startup.CheckAsync() || windowClosed) return;
        var storage = new ProjectStorageOptions();
        var repository = new JsonProjectRepository(storage);
        var paths = new PiSessionPaths(storage);
        var startInfo = new PiProcessStartInfoFactory(locator);
        workspaces = new ConversationWorkspaceStore((project, conversation) =>
            new ConversationSession(new PiLaunchRequest(project.Path, paths.GetSessionFile(project.Id, conversation.Id),
                conversation.IsTitleManual ? conversation.Title : null),
                () => new PiRpcClient(new ProcessPiTransport(startInfo), runtime.RequestTimeout)),
            new DispatcherQueueUiDispatcher(window.DispatcherQueue));
        var projectService = new ProjectService(repository);
        var shell = new ShellViewModel(repository, workspaces);
        window.Title = shell.WindowTitle;
        shell.PropertyChanged += (_, change) =>
        {
            if (!windowClosed && change.PropertyName == nameof(ShellViewModel.WindowTitle)) window.Title = shell.WindowTitle;
        };
        var picker = new FolderPickerService(WinRT.Interop.WindowNative.GetWindowHandle(window));
        window.Content = new MainPage(shell, () => new CreateProjectViewModel(projectService), picker);
    }

    private async void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (canClose) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        try
        {
            shutdown ??= workspaces?.DisposeAsync().AsTask() ?? Task.CompletedTask;
            await shutdown;
        }
        catch (Exception)
        {
            // Keep the window open so a shutdown failure is visible rather than silently abandoning a process.
            var dialog = new Controls.ActionContentDialog
            {
                XamlRoot = (window!.Content as FrameworkElement)!.XamlRoot,
                Title = "Couldn't close the agent processes",
                Content = "An agent process did not shut down cleanly and may still be running. Close the app anyway?",
                PrimaryButtonText = "Close app",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) { closing = false; return; }
        }
        canClose = true;
        window?.Close();
    }
}

