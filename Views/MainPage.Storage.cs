using PiAgentGui.Controls;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private async Task HandleStorageActionAsync(SettingsViewModel settings, string action)
    {
        if (settings.StorageService is not { } storage) return;
        if (action == "storage-refresh") { await settings.RefreshStorageAsync(reportSuccess: true); return; }
        dialogOpen = true;
        try
        {
            if (action == "storage-folder")
            {
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(storage.AppDirectory);
                if (!await Windows.System.Launcher.LaunchFolderAsync(folder)) throw new IOException("The folder could not be opened.");
                settings.SetFeedback("App data folder opened.", SettingsFeedbackKind.Success);
                return;
            }
            if (action is "storage-diagnostics" or "storage-research")
            {
                var research = action == "storage-research";
                var confirmation = new ActionContentDialog
                {
                    XamlRoot = XamlRoot, Title = research ? "Clear completed research?" : "Clear diagnostics?",
                    Content = research ? "Delete completed research results and their worker transcripts. Running, queued, failed and interrupted tasks are kept. Answers already shared in conversations remain. This cannot be undone."
                        : "Delete the saved app crash report. Conversation history and project files are kept.",
                    PrimaryButtonText = "Clear", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
                };
                if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
            }
            settings.Busy = true;
            settings.SetFeedback("Cleaning up…");
            switch (action)
            {
                case "storage-diagnostics":
                    await storage.ClearDiagnosticsAsync(); settings.SetFeedback("Saved crash diagnostics cleared.", SettingsFeedbackKind.Success); break;
                case "storage-research":
                    if (settings.ClearCompletedResearch is not { } clear) throw new IOException("Research storage is unavailable.");
                    settings.SetFeedback($"Cleared {await clear()} completed research tasks.", SettingsFeedbackKind.Success); break;
                case "storage-retry":
                    if (ViewModel.HasActiveWork || Research.HasActiveTasks || Terminals.Tabs.Any(tab => !tab.IsFinished))
                        throw new IOException("Finish active work and close running terminals before retrying cleanup.");
                    var result = await ViewModel.RetryCleanupAsync();
                    settings.SetFeedback(result, ViewModel.HasError ? SettingsFeedbackKind.Warning : SettingsFeedbackKind.Success); break;
            }
            await settings.RefreshStorageAsync();
        }
        catch (Exception error) { settings.SetFeedback("Storage action could not finish. " + error.Message, SettingsFeedbackKind.Error); }
        finally { settings.Busy = false; dialogOpen = false; }
    }
}
