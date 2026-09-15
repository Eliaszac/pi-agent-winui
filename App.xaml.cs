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
    private ProviderService? providers;
    private ViewModels.Terminal.TerminalPanelViewModel? terminals;
    private ResearchCoordinator? research;
    private readonly CancellationTokenSource githubLifetime = new();
    private readonly System.Net.Http.HttpClient githubHttp = new(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    private bool closing;
    private bool canClose;
    private Task? shutdown;
    private bool windowClosed;

    /// <summary>Initializes native application resources and system theming.</summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => CrashReportWriter.Write(args.Exception);
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var wslDistributions = new WslDistributionCache(new WslDistributionDiscovery().ReadAsync);
        _ = wslDistributions.GetAsync();
        window = new Window { Title = ApplicationIdentity.Name };
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Pi.ico"));
        window.AppWindow.TitleBar.PreferredTheme = Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        var runtime = PiRuntimeOptions.FromEnvironment();
        var locator = new PiInstallationLocator(runtime);
        var startup = new StartupViewModel(locator);
        window.Content = new StartupPage(startup);
        window.Closed += (_, _) => { windowClosed = true; githubLifetime.Cancel(); githubHttp.Dispose(); };
        window.AppWindow.Closing += OnClosing;
        window.Activate();
        if (!await startup.CheckAsync() || windowClosed) return;
        var storage = new ProjectStorageOptions();
        var repository = new JsonProjectRepository(storage);
        var paths = new PiSessionPaths(storage);
        var usageReader = new Services.Home.SessionUsageReader(paths);
        var defaultHistory = new ConversationDefaultsReader(repository, usageReader);
        var startInfo = new PiProcessStartInfoFactory(locator);
        var researchDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "research");
        var researchStore = new ResearchStore(researchDirectory);
        research = new ResearchCoordinator(researchStore, new PiResearchRunner(() => new PiRpcClient(new ProcessPiTransport(startInfo), runtime.RequestTimeout), researchDirectory));
        var researchPanel = new ViewModels.Conversations.ResearchPanelViewModel(research, new DispatcherQueueUiDispatcher(window.DispatcherQueue));
        try { await research.InitializeAsync(); }
        catch (Exception exception) { researchPanel.ReportError("Couldn't load saved research: " + exception.Message); }
        workspaces = new ConversationWorkspaceStore((project, conversation) =>
            new ConversationSession(new PiLaunchRequest(ProjectTargets.Resolve(project, conversation.TargetId ?? project.Id).Path, paths.GetSessionFile(project.Id, conversation.Id),
                conversation.IsTitleManual ? conversation.Title : null, ResearchPreferencePath: researchStore.PreferencePath,
                Target: ProjectTargets.Resolve(project, conversation.TargetId ?? project.Id)),
                () => new PiRpcClient(new ProcessPiTransport(startInfo), runtime.RequestTimeout))
            {
                ReadDefaultHistory = defaultHistory.ReadAsync,
                ResearchRequested = async payload => await research.DispatchAsync(new Models.Conversations.ResearchTask(Guid.NewGuid(), conversation.Id, project.Path,
                    PiJson.Text(payload, "title"), PiJson.Text(payload, "question"), PiJson.Text(payload, "provider"), PiJson.Text(payload, "model"),
                    PiJson.Text(payload, "effort"), "Queued", "", DateTimeOffset.UtcNow))
            },
            new DispatcherQueueUiDispatcher(window.DispatcherQueue), modelFavorites: new ModelFavoritesStore(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "model-favorites.json")));
        var projectService = new ProjectService(repository);
        workspaces.ViewedRunCompleted += () => { if (!closing && !windowClosed && Controls.ReadingPreferences.Current.CompletionAudio) CompletionSound.Play(); };
        var checkpointData = new CheckpointDataService(repository);
        var dockerClient = new Services.Docker.DockerCliClient();
        var docker = new ViewModels.Docker.DockerPanelViewModel(
            new Services.Docker.DockerPreferenceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "docker.json")),
            dockerClient, new Services.Docker.DockerSourceDiscovery(repository, wslDistributions, dockerClient));
        await docker.InitializeAsync();
        window.Closed += (_, _) => docker.Dispose();
        var extensions = new ViewModels.Extensions.ExtensionsViewModel(ViewModels.Extensions.SupportedExtensions.All
            .Append(ViewModels.Extensions.SupportedExtensions.Research(research)).Append(ViewModels.Extensions.SupportedExtensions.Docker(docker)));
        var shell = new ShellViewModel(repository, workspaces, paths, new ConversationDataCleanup(paths, repository, checkpointData.ForgetAsync, usageReader), extensions);
        providers = new ProviderService(() => new PiRpcClient(new ProcessPiTransport(startInfo), runtime.RequestTimeout),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "management"));
        var ollamaHttp = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        window.Closed += (_, _) => ollamaHttp.Dispose();
        var ollama = new ViewModels.Providers.OllamaSetupViewModel(new OllamaClient(ollamaHttp),
            new OllamaModelImporter(PermissionModesSupport.AgentDirectory), () =>
            {
                workspaces.InvalidateProviderModels();
                if (shell.Providers is { IsRefreshing: false } currentProviders) _ = currentProviders.RefreshCatalogAsync();
            }, githubLifetime.Token);
        shell.Providers = new ViewModels.Providers.ProvidersViewModel(providers, ollama);
        _ = ollama.SyncKnownAsync(githubLifetime.Token);
        providers.CredentialsChanged += () => window.DispatcherQueue.TryEnqueue(() => workspaces.InvalidateProviderModels());
        window.Title = shell.WindowTitle;
        shell.PropertyChanged += (_, change) =>
        {
            if (!windowClosed && change.PropertyName == nameof(ShellViewModel.WindowTitle)) window.Title = shell.WindowTitle;
        };
        var picker = new FolderPickerService(WinRT.Interop.WindowNative.GetWindowHandle(window));
        var openIn = new ViewModels.Applications.OpenInViewModel(new Services.Applications.InstalledApplicationLocator(),
            new Services.Applications.OpenInPreferenceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "open-in.json")),
            new Services.Applications.ProjectApplicationLauncher());
        var fileEditor = new Services.Applications.WorkspaceEditorLauncher(new Services.Applications.InstalledApplicationLocator(),
            new Services.Applications.OpenInPreferenceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "open-in.json")));
        workspaces.FileLinkFactory = target => new Services.Files.WorkspaceFileLinks(target, fileEditor.OpenAsync);
        workspaces.ArtifactFactory = (project, conversation) => new(
            new Services.Conversations.ArtifactStore(Services.Conversations.ArtifactStore.ForSession(paths.GetSessionFile(project.Id, conversation.Id))),
            ProjectTargets.Resolve(project, conversation.TargetId ?? project.Id));
        var githubOptions = GitHubOptions.Load();
        var githubApi = new Services.GitHub.GitHubApi(githubHttp);
        var github = new ViewModels.GitHub.GitHubViewModel(new Services.GitHub.GitHubAuthentication(githubOptions, githubApi,
            new Services.GitHub.WindowsGitHubCredentialStore(githubOptions.ClientId)), githubApi, new Services.GitHub.GitBranchReader());
        workspaces.GitHub = github;
        terminals = new(directory => new Services.Terminal.ConPtySession(directory),
            (directory, command) => new Services.Terminal.ConPtySession(directory, command));
        terminals.CreateTargetSession = (target, command) => new Services.Terminal.ConPtySession(target.Path, command, target);
        var files = new ViewModels.Files.FileExplorerViewModel(new DispatcherQueueUiDispatcher(window.DispatcherQueue));
        window.Closed += (_, _) => files.Dispose();
        var processReader = new AgentProcessReader();
        var processes = new ViewModels.Processes.ProcessesPanelViewModel(processReader, new AgentProcessStopper(processReader.Read, WindowsProcessTerminator.Stop));
        var sourceControl = new ViewModels.SourceControl.SourceControlViewModel(new Services.SourceControl.GitRepositoryService(new Services.SourceControl.GitCommandRunner()));
        window.Closed += (_, _) => sourceControl.Dispose();
        var scripts = new ViewModels.Projects.ProjectScriptsViewModel(repository, terminals);
        var agentDirectory = PermissionModesSupport.AgentDirectory;
        var imports = new CapabilityImportServices(new GlobalSkillRegistration(agentDirectory),
            new PiPackageInstaller(locator, agentDirectory, githubLifetime.Token), new McpConfigImporter(agentDirectory),
            new McpSetupService(agentDirectory), new McpTokenStore(locator, agentDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "mcp-management"), githubLifetime.Token),
            new McpConnectionService(() => new PiRpcClient(new ProcessPiTransport(startInfo), runtime.RequestTimeout),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "mcp-management"), githubLifetime.Token));
        var settingsStore = new Services.Settings.AppSettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "settings.json"));
        string settingsError = "";
        try { await settingsStore.LoadAsync(); }
        catch (Exception error) { settingsError = "Saved preferences could not be read; defaults are shown. " + error.Message; }
        shell.ResumeConversationOnStartup = settingsStore.Current.ResumeConversation;
        var settings = new ViewModels.Settings.SettingsViewModel(settingsStore, usageReader)
        {
            Message = settingsError,
            StorageService = new Services.Settings.StorageOverviewService(Path.GetDirectoryName(storage.CatalogPath)!,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi-desktop-checkpoints")),
            ClearCompletedResearch = research.ClearCompletedAsync
        };
        var sidebarStore = new Services.Settings.SidebarPreferencesStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "sidebar.json"));
        try
        {
            var savedSidebar = await sidebarStore.LoadAsync();
            shell.Sidebar.Restore(savedSidebar.Width, savedSidebar.IsOpen);
        }
        catch (Exception error) { settings.Message = "Saved sidebar layout could not be read. " + error.Message; }
        async Task SaveSidebarAsync()
        {
            try { await sidebarStore.SaveAsync(new(shell.Sidebar.PreferredWidth, shell.Sidebar.PreferredOpen)); }
            catch (Exception error) { settings.Message = "Sidebar layout could not be saved. " + error.Message; }
        }
        void SidebarPreferenceChanged(object? sender, EventArgs args) => sidebarSaveTask = SaveSidebarAsync();
        shell.Sidebar.PreferenceChanged += SidebarPreferenceChanged;
        window.Closed += (_, _) => shell.Sidebar.PreferenceChanged -= SidebarPreferenceChanged;
        if (settingsStore.Current.UsageResetAt is { } usageCutoff)
        {
            try { await usageReader.ResetAsync(usageCutoff); }
            catch (Exception error) { settings.Message = "Could not apply the saved usage reset. " + error.Message; }
        }
        var home = new ViewModels.Home.HomeViewModel(shell, usageReader, settingsStore);
        Controls.ReadingPreferences.Apply(settingsStore.Current);
        window.Content = new MainPage(shell, () => new CreateProjectViewModel(projectService), picker, openIn, github, githubOptions, githubLifetime.Token, terminals, researchPanel, files, processes, sourceControl, scripts, imports, repository, wslDistributions, docker, home, settings);
        void ApplyPreferences(object? sender, EventArgs args)
        {
            Controls.ReadingPreferences.Apply(settingsStore.Current);
            Controls.TerminalPreferences.Apply(settingsStore.Current);
            if (window.Content is FrameworkElement root) root.RequestedTheme = (ElementTheme)settingsStore.Current.Theme;
            window.AppWindow.TitleBar.PreferredTheme = settingsStore.Current.Theme switch
            {
                1 => Microsoft.UI.Windowing.TitleBarTheme.Light,
                2 => Microsoft.UI.Windowing.TitleBarTheme.Dark,
                _ => Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode
            };
            shell.ResumeConversationOnStartup = settingsStore.Current.ResumeConversation;
        }
        settingsStore.Changed += ApplyPreferences;
        ApplyPreferences(null, EventArgs.Empty);
        window.Closed += (_, _) => settingsStore.Changed -= ApplyPreferences;
    }

    private Task sidebarSaveTask = Task.CompletedTask;
    private async void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (canClose) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        try
        {
            var researchCount = research is null ? 0 : await research.GetPendingCountAsync();
            var runCount = workspaces?.ActiveRunCount ?? 0;
            if (runCount > 0 || researchCount > 0)
            {
                var dialog = new Controls.ActionContentDialog
                {
                    XamlRoot = (window!.Content as FrameworkElement)!.XamlRoot,
                    Title = "Work is still running",
                    Content = $"Active conversations: {runCount}\nRunning or queued research tasks: {researchCount}\n\nClosing Pi desktop will stop this work and close any open terminals.",
                    PrimaryButtonText = "Stop and close",
                    CloseButtonText = "Keep open",
                    DefaultButton = ContentDialogButton.Close
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) { closing = false; return; }
            }
        }
        catch (Exception)
        {
            // Another modal may already own the XamlRoot. Never stop work when confirmation could not be shown.
            closing = false;
            return;
        }
        (window?.Content as MainPage)?.CloseTerminalDisplays();
        try
        {
            shutdown ??= Task.WhenAll(workspaces?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                research?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                terminals?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                providers?.DisposeAsync().AsTask() ?? Task.CompletedTask);
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
            try { if (await dialog.ShowAsync() != ContentDialogResult.Primary) { closing = false; return; } }
            catch (Exception) { closing = false; return; }
        }
        await sidebarSaveTask;
        canClose = true;
        window?.Close();
    }
}

