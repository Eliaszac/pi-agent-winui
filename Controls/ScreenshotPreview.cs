using PiAgentGui.Models.Conversations;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace PiAgentGui.Controls;

/// <summary>Decodes a session image only while its preview is mounted.</summary>
public sealed class ScreenshotPreview : UserControl
{
    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ChatImage),
        typeof(ScreenshotPreview), new PropertyMetadata(null, (sender, _) => ((ScreenshotPreview)sender).Refresh()));
    public ChatImage? Image { get => (ChatImage?)GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    private readonly Image display = new() { Stretch = Stretch.Uniform, MaxHeight = 180, MaxWidth = 320 };
    private int version;

    public ScreenshotPreview()
    {
        Content = display;
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => { version++; display.Source = null; };
    }

    private async void Refresh()
    {
        if (!IsLoaded) return;
        var current = ++version;
        display.Source = null;
        if (Image is not { } image) return;
        try
        {
            var bytes = Convert.FromBase64String(image.Data);
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
            }
            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = 640 };
            await bitmap.SetSourceAsync(stream);
            if (current == version && IsLoaded) display.Source = bitmap;
        }
        catch (Exception)
        {
            if (current == version) ToolTipService.SetToolTip(this, "Screenshot preview unavailable");
        }
    }
}
