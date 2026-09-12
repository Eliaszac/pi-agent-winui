using PiAgentGui.Services.Projects;

namespace PiAgentGui.Controls;

public sealed class WslDistributionPicker : UserControl
{
    private readonly ComboBox choice = new() { Header = "WSL distribution", PlaceholderText = "Choose a distribution", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ActionButton refresh = new() { Content = new FontIcon { Glyph = "\uE72C", FontSize = 14 }, VerticalAlignment = VerticalAlignment.Bottom, Width = 34, Height = 34, MinWidth = 34, Padding = new Thickness(8) };
    private WslDistributionCache? cache;
    private Action<string>? selected;

    public WslDistributionPicker()
    {
        var layout = new StackPanel { Spacing = 6 };
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        row.Children.Add(choice); Grid.SetColumn(refresh, 1); row.Children.Add(refresh);
        ToolTipService.SetToolTip(refresh, "Refresh distributions");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(refresh, "Refresh distributions");
        status.FontSize = 12;
        layout.Children.Add(row); layout.Children.Add(status);
        Content = layout;
        choice.SelectionChanged += (_, _) => selected?.Invoke(choice.SelectedItem as string ?? "");
        refresh.Click += async (_, _) => await LoadAsync(true);
    }

    public void Bind(WslDistributionCache source, Action<string> onSelected)
    {
        cache = source; selected = onSelected;
        _ = LoadAsync(false);
    }

    private async Task LoadAsync(bool reload)
    {
        if (cache is null) return;
        var previous = choice.SelectedItem as string;
        choice.IsEnabled = false; refresh.IsEnabled = false;
        status.Text = "Checking installed distributions…";
        status.Visibility = Visibility.Visible;
        var snapshot = await cache.GetAsync(reload);
        choice.ItemsSource = snapshot.Names;
        choice.SelectedItem = previous is not null && snapshot.Names.Contains(previous) ? previous : snapshot.PreferredName;
        choice.IsEnabled = snapshot.Names.Count > 0; refresh.IsEnabled = true;
        status.Text = snapshot.Error ?? (snapshot.Names.Count == 0
            ? "No distributions found. Install Ubuntu or another Linux distribution with WSL, then refresh. See learn.microsoft.com/windows/wsl/install."
            : "");
        status.Visibility = status.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        selected?.Invoke(choice.SelectedItem as string ?? "");
    }
}
