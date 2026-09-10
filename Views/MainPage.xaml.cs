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

    /// <summary>Creates the main view with dependencies composed by App.</summary>
    /// <param name="viewModel">The UI-independent shell state.</param>
    /// <param name="createProjectForm">Creates a fresh modal form.</param>
    /// <param name="picker">The window-owned folder picker.</param>
    public MainPage(ShellViewModel viewModel, Func<CreateProjectViewModel> createProjectForm, FolderPickerService picker)
    {
        ViewModel = viewModel;
        this.createProjectForm = createProjectForm;
        this.picker = picker;
        InitializeComponent();
        ViewModel.Sidebar.PropertyChanged += OnSidebarChanged;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ShellViewModel.HeaderRename) or nameof(ShellViewModel.ShowExtensions)) HeaderPath.HidePath();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (initialized) return;
        initialized = true;
        await ViewModel.LoadAsync();
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

