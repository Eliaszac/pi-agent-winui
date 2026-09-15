using Microsoft.UI.Xaml.Media.Imaging;
using PiAgentGui.Controls;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace PiAgentGui.Services.Dialogs;

public sealed class ArtifactActions
{
    public async Task ExecuteAsync(XamlRoot root, ArtifactItemViewModel item, string action)
    {
        if (action == "attach")
        {
            if (item.Owner.AttachToMessage is not { } attach) throw new IOException("This conversation is unavailable.");
            attach(item);
            return;
        }
        if (action == "rename")
        {
            var input = new TextBox { Text = item.Name, Header = "Filename", MaxLength = 180 };
            var dialog = new ActionContentDialog { XamlRoot = root, Title = "Rename artifact", Content = input, PrimaryButtonText = "Rename", CloseButtonText = "Cancel" };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                try { Services.Conversations.ArtifactStore.ValidateName(input.Text); }
                catch (IOException error) { input.Header = error.Message; args.Cancel = true; }
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            await item.Owner.Store.RenameAsync(item.Id, input.Text);
            await item.Owner.RefreshAsync();
            return;
        }
        if (action == "delete")
        {
            var dialog = new ActionContentDialog { XamlRoot = root, Title = "Delete artifact?", Content = $"Delete {item.Name} from this conversation's artifacts? Saved copies and original source files are kept. The card will show that it was deleted.", PrimaryButtonText = "Delete", CloseButtonText = "Cancel" };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            await item.Owner.DeleteAsync(item.Id);
            return;
        }
        var path = await item.Owner.Store.GetPathAsync(item.Id);
        if (action == "save") { await SaveAsync(root, item.Name, path); return; }
        if (action == "open")
        {
            if (!await Windows.System.Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path))) throw new IOException("No application could open this file. Save a copy and choose an application.");
            return;
        }
        if (action != "preview") return;
        object content;
        if (item.Record.MimeType is "image/png" or "image/jpeg" or "image/webp")
            content = new Image { Source = new BitmapImage(new Uri(path)), MaxHeight = 560, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
        else if (ArtifactFileTypes.IsText(item.Name))
        {
            using var reader = File.OpenText(path);
            var buffer = new char[128 * 1024];
            var count = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            content = new TextBox { Text = new string(buffer, 0, count) + (reader.Peek() >= 0 ? "\n\n[Preview truncated — save a copy to read the complete file.]" : ""), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 560, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"), FontSize = 12 };
        }
        else content = new TextBlock { Text = "Open this file in its default application, or save a copy to your computer.", TextWrapping = TextWrapping.Wrap };
        var preview = new ActionContentDialog { XamlRoot = root, Title = item.Name, Content = content, CloseButtonText = "Close", PrimaryButtonText = "Open in default app" };
        if (await preview.ShowAsync() == ContentDialogResult.Primary) await ExecuteAsync(root, item, "open");
    }

    private static async Task SaveAsync(XamlRoot root, string name, string source)
    {
        var extension = Path.GetExtension(name);
        var picker = new FileSavePicker { SuggestedFileName = name, SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeChoices.Add("Artifact", new List<string> { extension.Length > 0 ? extension : ".bin" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Microsoft.UI.Win32Interop.GetWindowFromWindowId(root.ContentIslandEnvironment.AppWindowId));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        if (string.Equals(file.Path, source, StringComparison.OrdinalIgnoreCase)) return;
        CachedFileManager.DeferUpdates(file);
        try
        {
            await using var input = File.OpenRead(source);
            using var output = await file.OpenStreamForWriteAsync();
            output.SetLength(0);
            await input.CopyToAsync(output);
        }
        finally
        {
            var status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status is not (FileUpdateStatus.Complete or FileUpdateStatus.CompleteAndRenamed)) throw new IOException("The selected storage provider could not finish saving the artifact.");
        }
    }
}
