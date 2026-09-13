namespace PiAgentGui.Controls;

/// <summary>Identifies provider choices with a neutral native symbol; names carry the product identity.</summary>
public sealed class ProviderLogo : ContentControl
{
    public static readonly DependencyProperty ProviderProperty = DependencyProperty.Register(nameof(Provider), typeof(string),
        typeof(ProviderLogo), new PropertyMetadata(null, (sender, _) => ((ProviderLogo)sender).Refresh()));
    public string? Provider { get => (string?)GetValue(ProviderProperty); set => SetValue(ProviderProperty, value); }
    public ProviderLogo()
    {
        IsTabStop = false;
        Loaded += (_, _) => Refresh();
        ActualThemeChanged += (_, _) => Refresh();
    }
    private void Refresh()
    {
        Content = new FontIcon { Glyph = Provider == "" ? "\uE734" : "\uE774", FontSize = 16 };
    }
}
