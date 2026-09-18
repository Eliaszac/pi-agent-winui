using PiAgentGui.Controls;
using PiAgentGui.Models.Settings;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private async Task RestoreSettingsDefaultsAsync(SettingsViewModel settings, string categoryName)
    {
        if (!Enum.TryParse<SettingsDefaultsCategory>(categoryName, out var category) || !Enum.IsDefined(category)) return;
        dialogOpen = true;
        try
        {
            var confirmation = new ActionContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Restore " + SettingsDefaults.Title(category) + " defaults?",
                Content = new TextBlock
                {
                    Text = SettingsDefaults.Description(category) + "\n\nOther categories, conversations and project files are preserved.",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "Restore defaults",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
            await settings.RestoreDefaultsAsync(category);
        }
        catch (Exception error)
        {
            settings.SetFeedback("Defaults could not be restored. " + error.Message, SettingsFeedbackKind.Error);
        }
        finally
        {
            dialogOpen = false;
            SettingsPane.SetEditors(OpenIn.EditorOptions, OpenIn.PreferredEditor);
        }
    }
}
