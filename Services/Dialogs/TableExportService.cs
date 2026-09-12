using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace PiAgentGui.Services.Dialogs;

/// <summary>Saves a reviewed table through the native destination picker.</summary>
public sealed class TableExportService
{
    public async Task<bool> SaveAsync(XamlRoot root, string format, string content)
    {
        if (format is not ("csv" or "json")) throw new ArgumentException("Unsupported table format.", nameof(format));
        var picker = new FileSavePicker { SuggestedFileName = "table", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add(format == "csv" ? "CSV table" : "JSON table", new List<string> { "." + format });
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            Microsoft.UI.Win32Interop.GetWindowFromWindowId(root.ContentIslandEnvironment.AppWindowId));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return false;
        CachedFileManager.DeferUpdates(file);
        try { await FileIO.WriteTextAsync(file, content, Windows.Storage.Streams.UnicodeEncoding.Utf8); }
        finally
        {
            var status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status != FileUpdateStatus.Complete && status != FileUpdateStatus.CompleteAndRenamed)
                throw new IOException("The selected storage provider could not finish saving the table.");
        }
        return true;
    }
}
