using Windows.Storage.Pickers;

namespace PiAgentGui.Services.Dialogs;

public sealed class PackageJsonPicker
{
    public async Task<string?> PickAsync(XamlRoot root, string buttonText = "Preview scripts")
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder, CommitButtonText = buttonText };
        picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            Microsoft.UI.Win32Interop.GetWindowFromWindowId(root.ContentIslandEnvironment.AppWindowId));
        return (await picker.PickSingleFileAsync())?.Path;
    }
}
