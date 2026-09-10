using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PiAgentGui.Services.Dialogs;

/// <summary>Normalizes an explicitly pasted clipboard bitmap into a bounded PNG payload.</summary>
public sealed class ClipboardScreenshotReader
{
    public async Task<ChatImage?> ReadAsync(DataPackageView clipboard)
    {
        if (!clipboard.Contains(StandardDataFormats.Bitmap)) return null;
        var reference = await clipboard.GetBitmapAsync();
        using var input = await reference.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(input);
        if ((ulong)decoder.PixelWidth * decoder.PixelHeight > 40_000_000)
            throw new InvalidOperationException("This screenshot is too large. Crop it before pasting.");
        var scale = Math.Min(1d, 2560d / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)) },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        if (output.Size > PiImageContent.MaximumImageBytes)
            throw new InvalidOperationException("This screenshot exceeds 2 MB. Crop it before pasting.");
        output.Seek(0);
        using var reader = new DataReader(output);
        await reader.LoadAsync((uint)output.Size);
        var bytes = new byte[(int)output.Size];
        reader.ReadBytes(bytes);
        return new(Convert.ToBase64String(bytes));
    }
}
