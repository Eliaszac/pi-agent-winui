using PiAgentGui.Controls;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private void OnSettingsClicked(object sender, RoutedEventArgs args) => ViewModel.OpenSettings();
    private void InitializeSettings(SettingsViewModel settings)
    {
        SettingsPane.DataContext = settings;
        LegalPane.BackRequested += (_, _) => ViewModel.OpenSettings();
        SettingsPane.ActionRequested += async (_, action) =>
        {
            if (action == "legal") { ViewModel.OpenSettings(legal: true); return; }
            if (action == "extensions") { ViewModel.OpenExtensions(); return; }
            if (dialogOpen || settings.Busy) return;
            var usage = action == "usage";
            var projects = action == "projects";
            if (!usage && (!ViewModel.CanManageSidebar || ViewModel.HasActiveWork || Research.HasActiveTasks || Terminals.Tabs.Any(tab => !tab.IsFinished)))
            {
                settings.Message = "Finish or cancel active conversations and research, and close running terminal tabs before deleting data.";
                return;
            }
            dialogOpen = true;
            try
            {
                var count = ViewModel.Projects.Sum(project => project.Conversations.Count);
                var content = new StackPanel { Spacing = 12 };
                content.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = usage
                    ? "Start the displayed totals from now. Earlier usage remains in your Pi session files, and your conversations are preserved. This does not reset provider usage or billing."
                    : $"Delete {count} conversations" + (projects ? $" and remove {ViewModel.Projects.Count} project registrations" : "") + "? This cannot be undone. Project files, shared Pi configuration, provider credentials and research records remain. Associated session screenshots and restore data are cleaned up; unavailable targets may leave cleanup pending." });
                var confirmation = new TextBox { PlaceholderText = "Type DELETE to confirm" };
                if (!usage) { Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(confirmation, "Type DELETE to confirm"); content.Children.Add(confirmation); }
                var dialog = new ActionContentDialog
                {
                    XamlRoot = XamlRoot, Title = usage ? "Reset local usage totals?" : projects ? "Remove all projects?" : "Delete all conversations?",
                    Content = content, PrimaryButtonText = usage ? "Reset totals" : "Delete", CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close, IsPrimaryButtonEnabled = usage
                };
                confirmation.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = confirmation.Text == "DELETE";
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                if (usage) { await settings.ResetUsageAsync(); return; }
                if (ViewModel.HasActiveWork || Research.HasActiveTasks || Terminals.Tabs.Any(tab => !tab.IsFinished))
                    throw new InvalidOperationException("Work started while confirmation was open. Finish it before deleting data.");
                settings.Busy = true;
                await ViewModel.ClearCatalogAsync(projects);
                settings.Message = ViewModel.HasError ? ViewModel.ErrorMessage : "The selected records were deleted. Your project files were preserved.";
            }
            catch (Exception error) { settings.Message = "The action could not be completed. " + error.Message; }
            finally { settings.Busy = false; dialogOpen = false; ViewModel.OpenSettings(); }
        };
    }
}
