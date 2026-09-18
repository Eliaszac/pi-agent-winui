using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using PiAgentGui.Controls;
using PiAgentGui.ViewModels.Settings;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private async Task PreviewSettingsDiagnosticsAsync(SettingsViewModel settings)
    {
        dialogOpen = true;
        try
        {
            // Capture once so Copy always uses the report that the user reviewed.
            var summary = settings.CreateDiagnosticSummary();
            var preview = new TextBox
            {
                Text = summary,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 12,
                MaxHeight = 360,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(preview, "Diagnostic summary to copy");
            ScrollViewer.SetVerticalScrollBarVisibility(preview, ScrollBarVisibility.Auto);
            var feedback = new InfoBar { IsOpen = false, IsClosable = false };
            AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock
            {
                Text = "Review the complete summary below. Copy copies only this text; the app does not send it anywhere. Windows clipboard history or sync may retain or share it according to your settings.",
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(preview);
            content.Children.Add(feedback);
            var dialog = new ActionContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Diagnostic summary",
                Content = content,
                PrimaryButtonText = "Copy summary",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                args.Cancel = true;
                try
                {
                    var data = new DataPackage();
                    data.SetText(summary);
                    Clipboard.SetContent(data);
                    feedback.Severity = InfoBarSeverity.Success;
                    feedback.Message = "The displayed summary was copied.";
                }
                catch (Exception)
                {
                    feedback.Severity = InfoBarSeverity.Error;
                    feedback.Message = "Could not copy the summary. Try again, or select and copy the text manually.";
                }
                feedback.IsOpen = true;
            };
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            settings.SetFeedback("The diagnostic preview could not be opened. Try again.", SettingsFeedbackKind.Error);
        }
        finally { dialogOpen = false; }
    }
}
