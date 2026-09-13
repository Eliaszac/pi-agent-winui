using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PiAgentGui.Controls;

public static class ApplicationLogoSource
{
    public static ImageSource Create(string asset, ElementTheme theme)
    {
        asset = asset switch
        {
            "cursor-light.svg" or "cursor-dark.svg" or "visualstudio.svg" or "zed.png" => "editor-light.svg",
            "clion.svg" or "goland.svg" or "idea.svg" or "phpstorm.svg" or "pycharm.svg" or "rider.svg"
                or "rubymine.svg" or "rustrover.svg" or "webstorm.svg" => "editor-light.svg",
            "terminal.svg" => "terminal-neutral-light.svg",
            "explorer.svg" => "folder-light.svg",
            _ => asset
        };
        if (theme == ElementTheme.Dark) asset = asset.Replace("-light.svg", "-dark.svg");
        var uri = new Uri("ms-appx:///Assets/OpenIn/" + asset);
        return asset.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? new SvgImageSource(uri) : new BitmapImage(uri);
    }
}
