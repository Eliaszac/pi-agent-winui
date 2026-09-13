using Microsoft.UI.Xaml.Media.Imaging;

namespace PiAgentGui.Controls;

public sealed class IntegrationLogo : ContentControl
{
    public static readonly DependencyProperty IntegrationProperty = DependencyProperty.Register(nameof(Integration), typeof(string),
        typeof(IntegrationLogo), new PropertyMetadata(null, (sender, _) => ((IntegrationLogo)sender).Refresh()));
    public string? Integration { get => (string?)GetValue(IntegrationProperty); set => SetValue(IntegrationProperty, value); }
    public IntegrationLogo()
    {
        IsTabStop = false;
        Loaded += (_, _) => Refresh();
        ActualThemeChanged += (_, _) => Refresh();
    }
    private void Refresh()
    {
        var dark = ActualTheme == ElementTheme.Dark;
        if (!new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast && Integration is "github" or "linear")
        {
            var path = Integration == "github" ? $"OpenIn/github-{(dark ? "dark" : "light")}.svg" : $"Integrations/linear-{(dark ? "dark" : "light")}.svg";
            Content = new Image { Source = new SvgImageSource(new Uri("ms-appx:///Assets/" + path)), Width = 26, Height = 26 };
            return;
        }
        Content = new FontIcon { Glyph = Integration switch { "obsidian" or "notion" => "\uE70B", "supabase" => "\uE968", _ => "\uE8A5" }, FontSize = 22 };
    }
}
