using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Dialogs;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private readonly ClipboardScreenshotReader screenshots = new();
    private bool readingScreenshot;
    private bool pickingFiles;

    private async void OnAttachFiles(object sender, RoutedEventArgs args)
    {
        if (pickingFiles || ViewModel is not { CanAttachFiles: true } owner) return;
        pickingFiles = true;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads, ViewMode = PickerViewMode.List };
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Microsoft.UI.Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId));
            var files = await picker.PickMultipleFilesAsync();
            if (files.Count > 0) await owner.UploadFilesAsync(files.Select(file => file.Path).ToArray());
        }
        catch (Exception error) { owner.ReportAttachmentError("Couldn't attach files. " + error.Message); }
        finally { pickingFiles = false; }
    }

    private async void OnRemoveFile(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: ArtifactItemViewModel item } || ViewModel is not { } owner) return;
        try { await owner.RemoveFileAsync(item); }
        catch (Exception error) { owner.ReportAttachmentError("Couldn't clean up the removed upload. " + error.Message); }
    }

    private void OnFilesDragOver(object sender, DragEventArgs args)
    {
        if (ViewModel?.CanAttachFiles != true || !args.DataView.Contains(StandardDataFormats.StorageItems)) return;
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.DragUIOverride.Caption = "Attach files";
        args.Handled = true;
    }

    private async void OnFilesDrop(object sender, DragEventArgs args)
    {
        if (ViewModel is not { CanAttachFiles: true } owner || !args.DataView.Contains(StandardDataFormats.StorageItems)) return;
        args.Handled = true;
        try { await AttachStorageItemsAsync(owner, args.DataView); }
        catch (Exception error) { owner.ReportAttachmentError("Couldn't attach files. " + error.Message); }
    }

    private static async Task AttachStorageItemsAsync(ConversationViewModel owner, DataPackageView data)
    {
        var items = await data.GetStorageItemsAsync();
        if (items.Any(item => item is not StorageFile)) throw new IOException("Attach individual files; folders are not supported.");
        await owner.UploadFilesAsync(items.Select(item => item.Path).ToArray());
    }

    private async void OnComposerPaste(object sender, TextControlPasteEventArgs args)
    {
        var owner = ViewModel;
        if (owner is null) return;
        if (readingScreenshot) { args.Handled = true; return; }
        try
        {
            var clipboard = Clipboard.GetContent();
            if (clipboard.Contains(StandardDataFormats.StorageItems))
            {
                args.Handled = true;
                readingScreenshot = true;
                await AttachStorageItemsAsync(owner, clipboard);
                return;
            }
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
