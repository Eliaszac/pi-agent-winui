using Windows.Storage.Pickers;

namespace PiAgentGui.Services.Dialogs;

public sealed class ConversationExportPicker
{
    public async Task<string?> PickAsync(XamlRoot root)
    {
        var windowId = root.ContentIslandEnvironment.AppWindowId;
        var picker = new FileSavePicker { SuggestedFileName = "conversation", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("HTML document", new List<string> { ".html" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Microsoft.UI.Win32Interop.GetWindowFromWindowId(windowId));
        return (await picker.PickSaveFileAsync())?.Path;
    }
}
