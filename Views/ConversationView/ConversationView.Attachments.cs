using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Dialogs;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private readonly ClipboardScreenshotReader screenshots = new();
    private bool readingScreenshot;

    private async void OnComposerPaste(object sender, TextControlPasteEventArgs args)
    {
        var owner = ViewModel;
        if (owner is null) return;
        if (readingScreenshot) { args.Handled = true; return; }
        try
        {
            var clipboard = Clipboard.GetContent();
            if (!clipboard.Contains(StandardDataFormats.Bitmap)) return;
            args.Handled = true;
            readingScreenshot = true;
            if (await screenshots.ReadAsync(clipboard) is { } image) owner.AddScreenshot(image);
        }
        catch (Exception exception) { owner.ReportAttachmentError("Couldn't paste screenshot. " + exception.Message); }
        finally { readingScreenshot = false; }
    }

    private void OnRemoveScreenshot(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ChatImage image }) ViewModel?.RemoveScreenshot(image);
    }
}
