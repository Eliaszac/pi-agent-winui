using PiAgentGui.Services.Dialogs;
using PiAgentGui.Services.Projects;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Controls;

public sealed partial class TargetWorkspaceForm : UserControl
{
    private FolderPickerService? picker;
    private TargetLocationViewModel? viewModel;
    public bool IsPicking { get; private set; }
    public TargetWorkspaceForm() => InitializeComponent();
    public void ClearSecret() { SshSecretBox.Password = ""; if (viewModel is not null) viewModel.SshSecret = ""; }
    private void OnSshSecretChanged(object sender, RoutedEventArgs args) { if (viewModel is not null) viewModel.SshSecret = SshSecretBox.Password; }
    public void Bind(TargetLocationViewModel model, FolderPickerService folders, WslDistributionCache distributions)
    {
        viewModel = model; picker = folders; DataContext = model;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(TargetLocationViewModel.SshAuthenticationIndex)) ClearSecret(); };
        WslPicker.Bind(distributions, name => model.WslDistribution = name);
    }
    private async void OnBrowseClicked(object sender, RoutedEventArgs args)
    {
        if (IsPicking || picker is null || viewModel is null) return;
        IsPicking = true; BrowseButton.IsEnabled = false; BrowseError.Visibility = Visibility.Collapsed;
        try { if (await picker.PickAsync() is { } path) viewModel.Path = path; }
        catch (Exception) { BrowseError.Text = "Couldn't open the folder picker. Enter the full path instead."; BrowseError.Visibility = Visibility.Visible; }
        finally { IsPicking = false; BrowseButton.IsEnabled = true; }
    }
}
