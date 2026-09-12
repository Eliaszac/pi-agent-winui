using Microsoft.UI.Xaml.Media.Imaging;
using PiAgentGui.Utilities;

namespace PiAgentGui.Controls;

/// <summary>Displays bundled provider brand artwork with a native fallback for custom providers.</summary>
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
        var icon = Provider is null ? null : ModelProviderPresentation.Get(Provider).Icon;
        if (icon is null || new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
        {
            Content = new FontIcon { Glyph = Provider == "" ? "\uE734" : "\uE774", FontSize = 16 };
            return;
        }
        var source = new SvgImageSource(new Uri($"ms-appx:///Assets/Providers/{icon}-{(ActualTheme == ElementTheme.Dark ? "dark" : "light")}.svg"));
        source.OpenFailed += (_, _) => Content = new FontIcon { Glyph = "\uE774", FontSize = 16 };
        Content = new Image { Source = source, Width = 20, Height = 20 };
    }
}
