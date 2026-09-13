using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class SettingsView : UserControl
{
    public event EventHandler<string>? ActionRequested;
    private SettingsViewModel? Model => DataContext as SettingsViewModel;
    public SettingsView() => InitializeComponent();
    private async void OnThemeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (IsLoaded && Model is { CanEdit: true } model && ThemeChoice.SelectedIndex >= 0 && ThemeChoice.SelectedIndex != model.ThemeIndex)
            await model.SetThemeAsync(ThemeChoice.SelectedIndex);
    }
    private async void OnSendKeyChanged(object sender, SelectionChangedEventArgs args)
    {
        if (IsLoaded && Model is { CanEdit: true } model && SendKeyChoice.SelectedIndex >= 0 && SendKeyChoice.SelectedIndex != model.SendKeyIndex)
            await model.SetSendKeyAsync(SendKeyChoice.SelectedIndex);
    }
    private async void OnBodySizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (IsLoaded && Model is { CanEdit: true } model && double.IsFinite(args.NewValue) && args.NewValue != model.ConversationTextSize)
            await model.SetTextSizeAsync(args.NewValue, false);
    }
    private async void OnCodeSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (IsLoaded && Model is { CanEdit: true } model && double.IsFinite(args.NewValue) && args.NewValue != model.CodeTextSize)
            await model.SetTextSizeAsync(args.NewValue, true);
    }
    private async void OnResetTextSizes(object sender, RoutedEventArgs args)
    {
        if (Model is { CanEdit: true } model) await model.ResetTextSizesAsync();
    }
    private bool updatingEditors;
    internal void SetEditors(IReadOnlyList<Models.Applications.EditorPreferenceOption> options, string? preferred)
    {
        updatingEditors = true;
        try
        {
            EditorChoice.ItemsSource = options;
            EditorChoice.SelectedItem = options.FirstOrDefault(option => option.Id == preferred) ?? options[0];
            EditorStatus.Text = options.Any(option => option.Id == preferred && !option.Available)
                ? "Your preferred editor is unavailable. Open in uses an available fallback until it is installed again."
                : "Uses the same choice as Open in. System default follows the project’s file association when available.";
        }
        finally { updatingEditors = false; }
    }
    private void OnEditorChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingEditors && IsLoaded && Model is { CanEdit: true } && EditorChoice.SelectedItem is Models.Applications.EditorPreferenceOption { Available: true } option)
            ActionRequested?.Invoke(this, "editor:" + option.Id);
    }
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
