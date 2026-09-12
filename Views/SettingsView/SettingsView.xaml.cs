using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class SettingsView : UserControl
{
    public event EventHandler<string>? ActionRequested;
    private SettingsViewModel? Model => DataContext as SettingsViewModel;
    public SettingsView() => InitializeComponent();
    private async void OnStartupChanged(object sender, SelectionChangedEventArgs args)
    {
        if (IsLoaded && Model is { CanEdit: true } model && Startup.SelectedIndex >= 0 && Startup.SelectedIndex != model.StartupIndex)
            await model.SetStartupAsync(Startup.SelectedIndex);
    }
    private async void OnUsageChanged(object sender, RoutedEventArgs args)
    {
        if (IsLoaded && Model is { CanEdit: true } model && UsageToggle.IsOn != model.ShowLocalUsage)
            await model.SetUsageAsync(UsageToggle.IsOn);
    }
    private void OnResetUsage(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, "usage");
    private void OnDataAction(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, (string)((FrameworkElement)sender).Tag);
    private void OnLegal(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, "legal");
}
