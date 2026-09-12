using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Views;

public sealed partial class CreateProjectDialog : Controls.ActionContentDialog
{
    private readonly Action<Project> onProjectCreated;
    public CreateProjectViewModel ViewModel { get; }
    public CreateProjectDialog(CreateProjectViewModel viewModel, FolderPickerService picker, Action<Project> onProjectCreated, Services.Projects.WslDistributionCache wslDistributions)
    {
        ViewModel = viewModel; this.onProjectCreated = onProjectCreated;
        InitializeComponent();
        WorkspaceForm.Bind(viewModel, picker, wslDistributions);
        Opened += (_, _) => { ResizeForm(); XamlRoot.Changed += OnRootChanged; ProjectName.Focus(FocusState.Programmatic); };
        Closed += (_, _) => { WorkspaceForm.ClearSecret(); if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged; };
    }
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ResizeForm();
    private void ResizeForm() => FormContent.Width = Math.Max(0, Math.Min(560, XamlRoot.Size.Width - 96));
    private async void OnCreateClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (WorkspaceForm.IsPicking) return;
        var deferral = args.GetDeferral();
        try
        {
            if (await ViewModel.TryCreateAsync() is { } project) { onProjectCreated(project); args.Cancel = false; }
        }
        finally { deferral.Complete(); }
    }
    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args) => args.Cancel = ViewModel.IsBusy || WorkspaceForm.IsPicking;
}
