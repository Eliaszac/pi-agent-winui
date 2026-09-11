using Microsoft.UI.Xaml.Media.Imaging;
using System.Text;

namespace PiAgentGui.Controls;

/// <summary>Loads vector file icons, adapting Visual Studio neutral strokes for dark backgrounds.</summary>
public static class FileTypeImageSource
{
    private static readonly Dictionary<(string, ElementTheme), Task<SvgImageSource>> sources = [];

    public static Task<SvgImageSource> CreateAsync(string name, ElementTheme theme)
    {
        if (!sources.TryGetValue((name, theme), out var source)) sources[(name, theme)] = source = LoadAsync(name, theme);
        return source;
    }

    private static async Task<SvgImageSource> LoadAsync(string name, ElementTheme theme)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "FileTypes", name + ".svg");
        var svg = await File.ReadAllTextAsync(path);
        if (theme == ElementTheme.Dark && !name.StartsWith("material-", StringComparison.Ordinal))
            svg = svg.Replace("#202020", "#D6D6D6", StringComparison.OrdinalIgnoreCase);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        var source = new SvgImageSource { RasterizePixelWidth = 128, RasterizePixelHeight = 128 };
        await source.SetSourceAsync(stream.AsRandomAccessStream());
        return source;
    }
}
