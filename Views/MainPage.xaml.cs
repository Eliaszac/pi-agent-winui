using System.ComponentModel;
using Microsoft.UI.Xaml.Input;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.ViewModels.Projects;
using PiAgentGui.ViewModels.Shell;
using PiAgentGui.Utilities;
using Windows.System;

namespace PiAgentGui.Views;

/// <summary>Composes the native sidebar, workspace, and modal interactions.</summary>
public sealed partial class MainPage : Page
{
    private readonly Func<CreateProjectViewModel> createProjectForm;
    private readonly FolderPickerService picker;
    private readonly Repositories.Projects.IProjectRepository projectRepository;
    private readonly Services.Projects.WslDistributionCache wslDistributions;
    private bool dialogOpen;
    private bool initialized;
    private double dragWidth;
    /// <summary>Gets the shell presentation state.</summary>
    public ShellViewModel ViewModel { get; }
    public ViewModels.Applications.OpenInViewModel OpenIn { get; }
    public ViewModels.GitHub.GitHubViewModel GitHub { get; }
    public ViewModels.Terminal.TerminalPanelViewModel Terminals { get; }
    public ProjectScriptsViewModel Scripts { get; }
    public ViewModels.Conversations.ResearchPanelViewModel Research { get; }
    public ViewModels.Docker.DockerPanelViewModel Docker { get; }
    public ViewModels.Files.FileExplorerViewModel Files { get; }
    public ViewModels.Processes.ProcessesPanelViewModel Processes { get; }
    public ViewModels.SourceControl.SourceControlViewModel SourceControl { get; }
    public ViewModels.Conversations.CapabilitiesPanelViewModel Capabilities { get; } = new();
    public GettingStartedViewModel GettingStarted { get; }
    private void OnGettingStartedProviders(object? sender, EventArgs args) => ViewModel.OpenProviders();
    private void OnWelcomeSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (WelcomeContent is not null) WelcomeContent.Width = Math.Max(0, Math.Min(600, args.NewSize.Width - 56));
        if (WelcomeViewport is not null) WelcomeViewport.MinHeight = args.NewSize.Height;
    }
    private readonly DispatcherTimer sourceControlTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer processesTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private double terminalWidth = 400;
    private readonly Configuration.GitHubOptions githubOptions;
    private readonly CancellationToken githubCancellation;
    private readonly DispatcherTimer githubTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    /// <summary>Creates the main view with dependencies composed by App.</summary>
    /// <param name="viewModel">The UI-independent shell state.</param>
    /// <param name="createProjectForm">Creates a fresh modal form.</param>
    /// <param name="picker">The window-owned folder picker.</param>
    public MainPage(ShellViewModel viewModel, Func<CreateProjectViewModel> createProjectForm, FolderPickerService picker, ViewModels.Applications.OpenInViewModel openIn,
        ViewModels.GitHub.GitHubViewModel github, Configuration.GitHubOptions githubOptions, CancellationToken githubCancellation,
        ViewModels.Terminal.TerminalPanelViewModel terminals, ViewModels.Conversations.ResearchPanelViewModel research,
        ViewModels.Files.FileExplorerViewModel files, ViewModels.Processes.ProcessesPanelViewModel processes,
        ViewModels.SourceControl.SourceControlViewModel sourceControl, ProjectScriptsViewModel scripts, Services.Pi.CapabilityImportServices imports,
        Repositories.Projects.IProjectRepository projectRepository, Services.Projects.WslDistributionCache wslDistributions,
        ViewModels.Docker.DockerPanelViewModel docker, ViewModels.Home.HomeViewModel home, ViewModels.Settings.SettingsViewModel settings)
    {
        ViewModel = viewModel;
        this.projectRepository = projectRepository;
        this.wslDistributions = wslDistributions;
        ViewModel.ChooseTargetAsync = ChooseConversationTargetAsync;
        GettingStarted = new(viewModel.Providers!, viewModel.Extensions,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "onboarding.json"));
        OpenIn = openIn;
        GitHub = github;
        Terminals = terminals;
        Scripts = scripts;
        Research = research;
        Docker = docker;
        Home = home;
        Files = files;
        Processes = processes;
        SourceControl = sourceControl;
        this.githubOptions = githubOptions;
        this.githubCancellation = githubCancellation;
        this.createProjectForm = createProjectForm;
        this.picker = picker;
        InitializeComponent();
        InitializeSettings(settings);
        InitializeHome();
        var sidebarRows = new SidebarRows(new Services.Windowing.DispatcherQueueUiDispatcher(DispatcherQueue));
        SidebarList.ItemsSource = sidebarRows.Rows;
        sidebarRows.SetProjects(ViewModel.Projects);
        ViewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ViewModel.Projects)) sidebarRows.SetProjects(ViewModel.Projects);
            if (change.PropertyName == nameof(ViewModel.Chat))
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (ViewModel.SelectedConversation is { } selected && sidebarRows.Rows.Contains(selected))
                        SidebarList.ScrollIntoView(selected);
                });
        };
        Unloaded += (_, _) => sidebarRows.Dispose();
        InitializeCommandPalette();
        CapabilitiesPane.DataContext = Capabilities;
        GettingStartedPane.DataContext = GettingStarted;
        GettingStartedPane.ProvidersRequested += (_, _) => ViewModel.OpenProviders();
        Unloaded += (_, _) => GettingStarted.Dispose();
        CapabilitiesPane.Imports = imports;
        CapabilitiesPane.PickSkillFolder = picker.PickAsync;
        Capabilities.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(Capabilities.IsOpen)) UpdateTerminalLayout(); };
        ViewModel.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(ViewModel.Chat)) Capabilities.Select(ViewModel.Chat?.IsRemoteTarget == true ? null : ViewModel.Chat); };
        Unloaded += (_, _) => { Capabilities.IsOpen = false; Capabilities.Select(null); };
        SourceControlPane.DataContext = SourceControl;
        sourceControlTimer.Tick += async (_, _) => await SourceControl.RefreshAsync();
        SourceControl.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName != nameof(SourceControl.IsOpen)) return;
            if (SourceControl.IsOpen) { sourceControlTimer.Start(); _ = SourceControl.RefreshAsync(); }
            else sourceControlTimer.Stop();
            UpdateTerminalLayout();
        };
        Unloaded += (_, _) => { sourceControlTimer.Stop(); SourceControl.IsOpen = false; };
        ProcessesPane.DataContext = Processes;
        processesTimer.Tick += async (_, _) => await RefreshProcessesAsync();
        Processes.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName != nameof(Processes.IsOpen)) return;
            if (Processes.IsOpen) { processesTimer.Start(); _ = RefreshProcessesAsync(); }
            else processesTimer.Stop();
            UpdateTerminalLayout();
        };
        ViewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ViewModel.Chat))
            {
                Processes.Select(ViewModel.SelectedTarget is { IsLocal: false } ? null : ViewModel.Chat?.ProcessIdentity);
                if (Processes.IsOpen) _ = RefreshProcessesAsync();
            }
        };
        Unloaded += (_, _) => { processesTimer.Stop(); Processes.IsOpen = false; };
        FilesPane.DataContext = Files;
        FilesPane.OpenFile = OpenTargetFileAsync;
        Files.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(Files.IsOpen)) UpdateTerminalLayout(); };
        ResearchPane.DataContext = Research;
        DockerPane.DataContext = Docker;
        InitializeDockerPanel();
        ResearchPane.ShareRequested += text => { if (ViewModel.Chat is { } chat) chat.Draft += (string.IsNullOrWhiteSpace(chat.Draft) ? "" : "\n\n") + text; };
        Research.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(Research.IsOpen)) UpdateTerminalLayout(); };
        ViewModel.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(ViewModel.Chat)) Research.SelectConversation(ViewModel.Chat?.ResearchOwnerId); };
        TerminalPane.Bind(Terminals);
        TerminalPane.CurrentDirectory = () => ViewModel.SelectedTarget?.Path;
        Terminals.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(Terminals.IsOpen)) UpdateTerminalLayout(); };
        InitializeSidePanels();
        WorkspaceContent.SizeChanged += (_, _) => UpdateTerminalLayout();
        OpenIn.PropertyChanged += (_, _) => UpdateOpenInLogo();
        ActualThemeChanged += (_, _) => { UpdateOpenInLogo(); UpdateGitHubLogo(); };
        UpdateGitHubLogo();
        GitHub.PullRequestChanged += (path, pull) =>
        {
            foreach (var conversation in ViewModel.Projects.SelectMany(project => project.Conversations)
                .Where(conversation => conversation.Target is { IsLocal: true } target && string.Equals(target.Path, path, StringComparison.OrdinalIgnoreCase)))
                conversation.SetPullRequest(pull);
        };
        githubTimer.Tick += async (_, _) => await RefreshGitHubAsync();
        Unloaded += (_, _) => githubTimer.Stop();
        OpenIn.Failed += message => { OpenInError.Message = message; OpenInError.IsOpen = true; };
        ViewModel.Sidebar.PropertyChanged += OnSidebarChanged;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ShellViewModel.HeaderRename) or nameof(ShellViewModel.ShowExtensions)) HeaderPath.HidePath();
            if (args.PropertyName == nameof(ShellViewModel.SelectedProject)) _ = OpenIn.RefreshAsync(ViewModel.LocalWorkspacePath);
            if (args.PropertyName == nameof(ShellViewModel.SelectedProject))
            {
                Terminals.Target = ViewModel.SelectedTarget;
                Processes.Select(ViewModel.SelectedTarget is { IsLocal: false } ? null : ViewModel.Chat?.ProcessIdentity);
                _ = Scripts.SelectAsync(ViewModel.SelectedProject?.Project.Id, ViewModel.SelectedTarget?.Path, ViewModel.SelectedTarget);
                Files.SelectTarget(ViewModel.SelectedTarget);
                SourceControl.SelectTarget(ViewModel.SelectedTarget);
                if (SourceControl.IsOpen) _ = SourceControl.RefreshAsync();
                GitHub.Select(ViewModel.LocalWorkspacePath);
                _ = RefreshGitHubAsync();
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (initialized) return;
        initialized = true;
        await ViewModel.LoadAsync();
        _ = GettingStarted.InitializeAsync();
        await Scripts.SelectAsync(ViewModel.SelectedProject?.Project.Id, ViewModel.SelectedTarget?.Path, ViewModel.SelectedTarget);
        Files.SelectTarget(ViewModel.SelectedTarget);
        await OpenIn.RefreshAsync(ViewModel.LocalWorkspacePath);
        GitHub.Initialize();
        GitHub.Select(ViewModel.LocalWorkspacePath);
        githubTimer.Start();
        await RefreshGitHubAsync();
    }

    private async Task RefreshGitHubAsync(bool force = false)
    {
        try { await GitHub.RefreshAsync(ViewModel.Projects.SelectMany(project => project.Targets).Where(target => target.IsLocal).Select(target => target.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), githubCancellation, force); }
        catch (OperationCanceledException) { }
        catch (Exception) { GitHub.ReportError("Couldn't refresh GitHub. Please try again."); }
    }

    private async void OnGitHubClicked(object sender, RoutedEventArgs args)
    {
        if (GitHub.IsConnected)
        {
            if (GitHub.SelectedPullRequest is { } pull) await OpenPullRequestAsync(pull.Url);
            return;
        }
        if (dialogOpen) return;
        dialogOpen = true;
        try { await new GitHubConnectDialog(GitHub, githubCancellation) { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception) { GitHub.ReportError("Couldn't open GitHub sign-in. Please try again."); }
        finally { dialogOpen = false; }
        await RefreshGitHubAsync(true);
    }

    private void OnConversationToolTipOpened(object sender, RoutedEventArgs args)
    {
        if (sender is ToolTip { Tag: ConversationItemViewModel conversation })
            conversation.RefreshPullRequestDetails();
    }

    private async void OnOpenPullRequestClicked(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: ConversationItemViewModel conversation } && conversation.PullRequestUrl is { } url)
            await OpenPullRequestAsync(url);
    }

    private async Task OpenPullRequestAsync(Uri url)
    {
        try { if (!await Launcher.LaunchUriAsync(url)) GitHub.ReportError("Couldn't open your browser."); }
        catch (Exception) { GitHub.ReportError("Couldn't open your browser."); }
    }

    private async void OnRefreshGitHubClicked(object sender, RoutedEventArgs args) => await RefreshGitHubAsync(true);
    private void OnDisconnectGitHubClicked(object sender, RoutedEventArgs args)
    {
        try { GitHub.Disconnect(); }
        catch (Exception) { GitHub.ReportError("Couldn't remove the GitHub login from Windows Credential Locker."); }
    }
    private async void OnManageGitHubClicked(object sender, RoutedEventArgs args) =>
        await OpenPullRequestAsync(githubOptions.InstallationUri ?? new Uri("https://github.com/settings/installations"));

    private void UpdateOpenInLogo() => OpenInLogo.Source = Controls.ApplicationLogoSource.Create(OpenIn.Logo, ActualTheme);

    private void OnTerminalClicked(object sender, RoutedEventArgs args) => OpenSidePanel("terminal");
    private void OnResearchClicked(object sender, RoutedEventArgs args) => OpenSidePanel("research");    private void OnTerminalResizeDelta(object? sender, double delta)
    {
        terminalWidth = Math.Clamp(terminalWidth - delta, 280, Math.Max(280, WorkspaceContent.ActualWidth * 0.6));
        UpdateTerminalLayout();
    }

    private void OnFilesClicked(object sender, RoutedEventArgs args) => OpenSidePanel("files");
    private void OnTerminalResizeKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is not (VirtualKey.Left or VirtualKey.Right)) return;
        OnTerminalResizeDelta(sender, args.Key == VirtualKey.Left ? -20 : 20);
        args.Handled = true;
    }
    private void UpdateTerminalLayout()
    {
        if (SidePanelHost is null) return;
        var wide = WorkspaceContent.ActualWidth >= 780;
        var width = Math.Min(terminalWidth, Math.Max(0, WorkspaceContent.ActualWidth * (wide ? 0.6 : 1)));
        var visible = activeSidePanels?.IsOpen == true && ViewModel.Chat is not null;
        SidePanelHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        TerminalColumn.Width = new GridLength(visible && wide ? width : 0);
        TerminalSplitterColumn.Width = new GridLength(visible && wide ? 6 : 0);
        Grid.SetColumn(SidePanelHost, wide ? 2 : 0);
        Grid.SetColumnSpan(SidePanelHost, wide ? 1 : 3);
        SidePanelHost.Width = width;
        SidePanelHost.HorizontalAlignment = HorizontalAlignment.Right;
    }
    public void CloseTerminalDisplays()
    {
        closingSidePanels = true;
        foreach (var surface in browserSurfaces.Values) surface.Dispose();
        browserSurfaces.Clear(); BrowserHost.Children.Clear();
        foreach (var tunnel in browserTunnels.Values) tunnel.Dispose();
        browserTunnels.Clear();
        sourceControlTimer.Stop(); SourceControl.Dispose();
        processesTimer.Stop(); Processes.IsOpen = false;
        dockerTimer.Stop(); Docker.Dispose();
        homeTimer.Stop(); Home.Dispose();
        TerminalPane.CloseDisplays();
    }

    private void OnSourceControlClicked(object sender, RoutedEventArgs args) => OpenSidePanel("source");
    private void OnProcessesClicked(object sender, RoutedEventArgs args) => OpenSidePanel("processes");    private async Task RefreshProcessesAsync()
    {
        Processes.Select(ViewModel.SelectedTarget is { IsLocal: false } ? null : ViewModel.Chat?.ProcessIdentity);
        await Processes.RefreshAsync();
    }

    private void UpdateGitHubLogo() => HeaderGitHubLogo.Source = Controls.ApplicationLogoSource.Create("github-light.svg", ActualTheme);

    private async void OnOpenInClicked(SplitButton sender, SplitButtonClickEventArgs args) => await OpenIn.OpenAsync();

    private void OnOpenInMenuOpening(object sender, object args)
    {
        OpenInMenu.Items.Clear();
        var editors = OpenIn.Applications.Where(app => app.Kind is Models.Applications.ApplicationKind.Editor or Models.Applications.ApplicationKind.SolutionEditor);
        var others = OpenIn.Applications.Where(app => app.Kind is Models.Applications.ApplicationKind.Explorer or Models.Applications.ApplicationKind.Terminal);
        foreach (var app in editors.Concat(others))
        {
            if (app == others.FirstOrDefault() && editors.Any()) OpenInMenu.Items.Add(new MenuFlyoutSeparator());
            var item = new Controls.ActionMenuFlyoutItem { Text = "Open in " + app.Name,
                Icon = new ImageIcon { Source = Controls.ApplicationLogoSource.Create(app.Logo, ActualTheme) } };
            item.Click += async (_, _) => await OpenIn.OpenAsync(app.Id);
            OpenInMenu.Items.Add(item);
        }
        OpenInMenu.Items.Add(new MenuFlyoutSeparator());
        var refresh = new Controls.ActionMenuFlyoutItem { Text = "Refresh applications", Icon = new FontIcon { Glyph = "\uE72C" } };
        refresh.Click += async (_, _) => await OpenIn.RefreshAsync(ViewModel.LocalWorkspacePath, true);
        OpenInMenu.Items.Add(refresh);
    }

    private async void OnNewProjectClicked(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || !ViewModel.CanManageSidebar) return;
        dialogOpen = true;
        try
        {
            var dialog = new CreateProjectDialog(createProjectForm(), picker, ViewModel.AddSavedProject, wslDistributions) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
        }
        catch (Exception exception) { ViewModel.ReportError(exception); }
        finally { dialogOpen = false; }
    }

    private void OnToggleSidebarClicked(object sender, RoutedEventArgs args) => ViewModel.Sidebar.Toggle();
    private void OnShellCommandRequested(string command, string argument)
    {
        switch (command)
        {
            case "new": ViewModel.NewConversationCommand.Execute(ViewModel.SelectedProject); break;
            case "name":
                ViewModel.HeaderRename.BeginCommand.Execute(null);
                if (!string.IsNullOrWhiteSpace(argument)) ViewModel.HeaderRename.Draft = argument;
                break;
            case "extensions": ViewModel.OpenExtensions(); break;
        }
    }
    private void OnExtensionsClicked(object sender, RoutedEventArgs args) => ViewModel.OpenExtensions();
    private void OnProvidersClicked(object sender, RoutedEventArgs args) => ViewModel.OpenProviders();
    private async void OnApprovalSetupRequested(object? sender, EventArgs args)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try { await new ExtensionSetupDialog { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception exception) { ViewModel.ReportError(exception); }
        finally { dialogOpen = false; }
    }

    private async void OnSettleConversationClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not MenuFlyoutItem { Tag: ConversationItemViewModel conversation }) return;
        try { await ViewModel.ToggleSettledAsync(conversation); }
        catch (Exception exception) { ViewModel.ReportError(exception); }
    }

    private async void OnDeleteConversationClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not MenuFlyoutItem { Tag: ConversationItemViewModel conversation }) return;
        await ConfirmDeletionAsync($"Delete {conversation.Title}?",
            "This deletes the conversation, its saved messages and screenshots, and unused checkpoint data, and stops its agent. Your project files are kept. Unavailable target cleanup will be retried; pending recovery data is protected.",
            () => ViewModel.DeleteConversationAsync(conversation));
    }

    private async void OnDeleteProjectClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not MenuFlyoutItem { Tag: ProjectItemViewModel project }) return;
        await ConfirmDeletionAsync($"Delete {project.Name}?",
            "This removes the project and deletes its conversations, saved messages, screenshots, and unused checkpoint data. Agents are stopped; your project folder is kept. Unavailable target cleanup will be retried; pending recovery data is protected.",
            () => ViewModel.DeleteProjectAsync(project));
    }

    private async Task ConfirmDeletionAsync(string title, string message, Func<Task> delete)
    {
        if (dialogOpen || !ViewModel.CanManageSidebar) return;
        dialogOpen = true;
        try
        {
            var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(feedback);
            var dialog = new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = title, Content = content,
                PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            dialog.PrimaryButtonClick += async (_, click) =>
            {
                var deferral = click.GetDeferral();
                try { await delete(); }
                catch (Exception exception)
                {
                    click.Cancel = true;
                    feedback.Text = ProjectErrorMessage.From(exception);
                    feedback.Visibility = Visibility.Visible;
                }
                finally { deferral.Complete(); }
            };
            await dialog.ShowAsync();
        }
        catch (Exception exception) { ViewModel.ReportError(exception); }
        finally { dialogOpen = false; }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.NewSize.Width <= 0) return;
        ViewModel.Sidebar.SetAvailableWidth(args.NewSize.Width);
        if (WorkspaceContent.Parent is Grid content) content.Padding = args.NewSize.Width < 760 ? new Thickness(16) : new Thickness(24, 16, 24, 16);
    }

    private void OnSidebarChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SidebarLayoutState.IsOverlay))
            WorkspaceSplit.DisplayMode = ViewModel.Sidebar.IsOverlay ? SplitViewDisplayMode.CompactOverlay : SplitViewDisplayMode.CompactInline;
    }

    private void OnResizeStarted(object? sender, EventArgs args) =>
        dragWidth = ViewModel.Sidebar.IsOpen ? ViewModel.Sidebar.ExpandedWidth : SidebarLayoutState.CollapsedWidth;

    private void OnResizeDelta(object? sender, double delta)
    {
        dragWidth = Math.Clamp(dragWidth + delta, 0, ViewModel.Sidebar.MaximumWidth);
        ViewModel.Sidebar.ResizeTo(dragWidth);
    }

    private void OnResizeKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var sidebar = ViewModel.Sidebar;
        switch (args.Key)
        {
            case VirtualKey.Left:
                if (sidebar.ExpandedWidth <= SidebarLayoutState.MinimumWidth) sidebar.IsOpen = false;
                else if (sidebar.IsOpen) sidebar.ResizeTo(sidebar.ExpandedWidth - 20);
                break;
            case VirtualKey.Right:
                if (!sidebar.IsOpen) sidebar.IsOpen = true;
                else sidebar.ResizeTo(sidebar.ExpandedWidth + 20);
                break;
            case VirtualKey.Home: sidebar.IsOpen = false; break;
            case VirtualKey.End: sidebar.IsOpen = true; break;
            default: return;
        }
        args.Handled = true;
    }
}


