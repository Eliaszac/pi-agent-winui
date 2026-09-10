using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Views;

/// <summary>Hosts the native create-project form and Windows folder picker.</summary>
public sealed partial class CreateProjectDialog : Controls.ActionContentDialog
{
    private readonly FolderPickerService picker;
    private readonly Action<Project> onProjectCreated;
    private bool isPicking;
    /// <summary>Gets the modal presentation state.</summary>
    public CreateProjectViewModel ViewModel { get; }

    /// <summary>Creates the modal for the current window.</summary>
    /// <param name="viewModel">The form state and save action.</param>
    /// <param name="picker">The window-owned folder picker.</param>
    /// <param name="onProjectCreated">Updates the workspace before the modal begins closing.</param>
    public CreateProjectDialog(CreateProjectViewModel viewModel, FolderPickerService picker, Action<Project> onProjectCreated)
    {
        ViewModel = viewModel;
        this.picker = picker;
        this.onProjectCreated = onProjectCreated ?? throw new ArgumentNullException(nameof(onProjectCreated));
        InitializeComponent();
        Opened += (_, _) => ProjectName.Focus(FocusState.Programmatic);
    }

    private async void OnBrowseClicked(object sender, RoutedEventArgs args)
    {
        if (isPicking) return;
        isPicking = true;
        BrowseButton.IsEnabled = false;
        try
        {
            var path = await picker.PickAsync();
            if (path is not null)
            {
                ViewModel.Path = path;
                if (string.IsNullOrWhiteSpace(ViewModel.Name))
                    ViewModel.Name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));
                ViewModel.ErrorMessage = "";
            }
        }
        catch (Exception) { ViewModel.ErrorMessage = "The folder picker could not open. Enter the full folder path instead."; }
        finally { isPicking = false; BrowseButton.IsEnabled = ViewModel.IsEditable; }
    }

    private async void OnCreateClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var deferral = args.GetDeferral();
        try
        {
            var project = await ViewModel.TryCreateAsync();
            if (project is not null)
            {
                // Commit presentation state while the modal still covers the old workspace.
                onProjectCreated(project);
                args.Cancel = false;
            }
        }
        finally { deferral.Complete(); }
    }

    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args) => args.Cancel = ViewModel.IsBusy || isPicking;
}

