using Windows.Storage.Pickers;

namespace PiAgentGui.Services.Dialogs;

public sealed class FileReferencePicker
{
    public async Task<string?> PickAsync(XamlRoot root)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            Microsoft.UI.Win32Interop.GetWindowFromWindowId(root.ContentIslandEnvironment.AppWindowId));
        return (await picker.PickSingleFileAsync())?.Path;
    }
}
