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
    private bool dialogOpen;
    private bool initialized;
    private double dragWidth;
    /// <summary>Gets the shell presentation state.</summary>
    public ShellViewModel ViewModel { get; }
    public ViewModels.Applications.OpenInViewModel OpenIn { get; }
    public ViewModels.GitHub.GitHubViewModel GitHub { get; }
    private readonly Configuration.GitHubOptions githubOptions;
    private readonly CancellationToken githubCancellation;
    private readonly DispatcherTimer githubTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    /// <summary>Creates the main view with dependencies composed by App.</summary>
    /// <param name="viewModel">The UI-independent shell state.</param>
    /// <param name="createProjectForm">Creates a fresh modal form.</param>
    /// <param name="picker">The window-owned folder picker.</param>
    public MainPage(ShellViewModel viewModel, Func<CreateProjectViewModel> createProjectForm, FolderPickerService picker, ViewModels.Applications.OpenInViewModel openIn,
        ViewModels.GitHub.GitHubViewModel github, Configuration.GitHubOptions githubOptions, CancellationToken githubCancellation)
    {
        ViewModel = viewModel;
        OpenIn = openIn;
        GitHub = github;
        this.githubOptions = githubOptions;
        this.githubCancellation = githubCancellation;
        this.createProjectForm = createProjectForm;
        this.picker = picker;
        InitializeComponent();
        OpenIn.PropertyChanged += (_, _) => UpdateOpenInLogo();
        ActualThemeChanged += (_, _) => { UpdateOpenInLogo(); UpdateGitHubLogo(); };
        UpdateGitHubLogo();
        GitHub.PullRequestChanged += (path, pull) =>
        {
            foreach (var project in ViewModel.Projects.Where(project => string.Equals(project.Path, path, StringComparison.OrdinalIgnoreCase)))
                foreach (var conversation in project.Conversations) conversation.SetPullRequest(pull);
        };
        githubTimer.Tick += async (_, _) => await RefreshGitHubAsync();
        Unloaded += (_, _) => githubTimer.Stop();
        OpenIn.Failed += message => { OpenInError.Message = message; OpenInError.IsOpen = true; };
        ViewModel.Sidebar.PropertyChanged += OnSidebarChanged;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ShellViewModel.HeaderRename) or nameof(ShellViewModel.ShowExtensions)) HeaderPath.HidePath();
            if (args.PropertyName == nameof(ShellViewModel.SelectedProject)) _ = OpenIn.RefreshAsync(ViewModel.SelectedProject?.Path);
            if (args.PropertyName == nameof(ShellViewModel.SelectedProject))
            {
                GitHub.Select(ViewModel.SelectedProject?.Path);
                _ = RefreshGitHubAsync();
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (initialized) return;
        initialized = true;
        await ViewModel.LoadAsync();
        await OpenIn.RefreshAsync(ViewModel.SelectedProject?.Path);
        GitHub.Initialize();
        GitHub.Select(ViewModel.SelectedProject?.Path);
        githubTimer.Start();
        await RefreshGitHubAsync();
    }

    private async Task RefreshGitHubAsync(bool force = false)
    {
        try { await GitHub.RefreshAsync(ViewModel.Projects.Select(project => project.Path).ToArray(), githubCancellation, force); }
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
            var item = new Controls.ActionMenuFlyoutItem { Text = app.Name,
                Icon = new ImageIcon { Source = Controls.ApplicationLogoSource.Create(app.Logo, ActualTheme) } };
            item.Click += async (_, _) => await OpenIn.OpenAsync(app.Id);
            OpenInMenu.Items.Add(item);
        }
        OpenInMenu.Items.Add(new MenuFlyoutSeparator());
        var refresh = new Controls.ActionMenuFlyoutItem { Text = "Refresh applications", Icon = new FontIcon { Glyph = "\uE72C" } };
        refresh.Click += async (_, _) => await OpenIn.RefreshAsync(ViewModel.SelectedProject?.Path, true);
        OpenInMenu.Items.Add(refresh);
    }

    private async void OnNewProjectClicked(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || !ViewModel.CanManageSidebar) return;
        dialogOpen = true;
        try
        {
            var dialog = new CreateProjectDialog(createProjectForm(), picker, ViewModel.AddSavedProject) { XamlRoot = XamlRoot };
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
            "This removes the conversation from the app and stops its running agent. The saved Pi session file is retained on disk.",
            () => ViewModel.DeleteConversationAsync(conversation));
    }

    private async void OnDeleteProjectClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not MenuFlyoutItem { Tag: ProjectItemViewModel project }) return;
        await ConfirmDeletionAsync($"Delete {project.Name}?",
            "This removes the project and its conversations from the app and stops their agents. Your project folder and saved Pi session files are retained on disk.",
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

