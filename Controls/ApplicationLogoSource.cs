using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PiAgentGui.Controls;

public static class ApplicationLogoSource
{
    public static ImageSource Create(string asset, ElementTheme theme)
    {
        if (theme == ElementTheme.Dark) asset = asset.Replace("-light.svg", "-dark.svg");
        var uri = new Uri("ms-appx:///Assets/OpenIn/" + asset);
        return asset.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? new SvgImageSource(uri) : new BitmapImage(uri);
    }
}
