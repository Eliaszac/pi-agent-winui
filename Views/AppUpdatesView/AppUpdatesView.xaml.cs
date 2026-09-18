using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class AppUpdatesView : UserControl
{
    public static readonly DependencyProperty SearchCategoryProperty = DependencyProperty.Register(
        nameof(SearchCategory), typeof(SettingsCategory), typeof(AppUpdatesView), new PropertyMetadata(null));
    public SettingsCategory? SearchCategory
    {
        get => (SettingsCategory?)GetValue(SearchCategoryProperty);
        set => SetValue(SearchCategoryProperty, value);
    }
    public event EventHandler? RestoreDefaultsRequested;
    public AppUpdatesView() => InitializeComponent();
    private void OnRestoreDefaults(object sender, RoutedEventArgs args) => RestoreDefaultsRequested?.Invoke(this, EventArgs.Empty);
    private async void OnAction(object sender, RoutedEventArgs args)
    {
        if (DataContext is AppUpdatesViewModel model) await model.ActAsync();
    }
    private async void OnPreferencesChanged(object sender, RoutedEventArgs args)
    {
        if (!IsLoaded || DataContext is not AppUpdatesViewModel { CanEditPreferences: true } model) return;
        if (ReferenceEquals(sender, CheckToggle) && CheckToggle.IsOn != model.AutomaticChecks)
            await model.SetPreferencesAsync(CheckToggle.IsOn, model.AutomaticDownloads);
        else if (ReferenceEquals(sender, DownloadToggle) && DownloadToggle.IsOn != model.AutomaticDownloads)
            await model.SetPreferencesAsync(model.AutomaticChecks, DownloadToggle.IsOn);
    }
}
