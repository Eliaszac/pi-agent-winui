using Windows.Storage.Pickers;

namespace PiAgentGui.Services.Dialogs;

/// <summary>Shows a Windows folder picker owned by the main application window.</summary>
public sealed class FolderPickerService(nint windowHandle)
{
    /// <summary>Lets the user choose an existing working directory.</summary>
    /// <returns>The chosen path, or null when the picker is cancelled.</returns>
    public async Task<string?> PickAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
