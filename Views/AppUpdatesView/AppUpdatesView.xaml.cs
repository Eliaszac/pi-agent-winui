using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class AppUpdatesView : UserControl
{
    public AppUpdatesView() => InitializeComponent();
    private async void OnAction(object sender, RoutedEventArgs args)
    {
        if (DataContext is AppUpdatesViewModel model) await model.ActAsync();
    }
    private async void OnPreferencesChanged(object sender, RoutedEventArgs args)
    {
        if (IsLoaded && DataContext is AppUpdatesViewModel model &&
            (CheckToggle.IsOn != model.AutomaticChecks || DownloadToggle.IsOn != model.AutomaticDownloads))
            await model.SetPreferencesAsync(CheckToggle.IsOn, DownloadToggle.IsOn);
    }
}
