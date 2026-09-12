using System.Text.Json;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private bool checkpointDialogOpen;
    private async void OnRevertChanges(object sender, RoutedEventArgs args) => await ShowCheckpointPreviewAsync(sender, false);
    private async void OnUndoRevert(object sender, RoutedEventArgs args) => await ShowCheckpointPreviewAsync(sender, true);
    private async void OnCheckpointRecovery(object sender, RoutedEventArgs args)
    {
        if (ViewModel is not { } vm || checkpointDialogOpen) return;
        checkpointDialogOpen = true;
        try
        {
            var confirm = new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = "Inspect checkpoint recovery",
                Content = "Stop any commands still running on the target first. Inspection checks the saved journal and current files without replaying writes.",
                PrimaryButtonText = "Inspect", CloseButtonText = "Cancel" };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary) await vm.CheckpointOperationAsync("recover");
        }
        catch (Exception error) { vm.ReportAttachmentError(error.Message); }
        finally { checkpointDialogOpen = false; }
    }
    private async Task ShowCheckpointPreviewAsync(object sender, bool undo)
    {
        if (ViewModel is not { } vm || checkpointDialogOpen || sender is not FrameworkElement { DataContext: RunChangesViewModel summary }) return;
        checkpointDialogOpen = true;
        try
        {
            if (summary.CheckpointId is null && !CheckpointSettings.IsEnabled())
            {
                await new ExtensionSetupDialog(ViewModels.Extensions.SupportedExtensions.Checkpoints) { XamlRoot = XamlRoot }.ShowAsync();
                return;
            }
            var preview = await vm.CheckpointOperationAsync("preview", summary, undo);
            var panel = new StackPanel { Spacing = 10, MaxWidth = 620 };
            panel.Children.Add(new TextBlock { Text = "Only selected files will be restored. Conversation history and Git staging are preserved. Files are checked again before applying.", TextWrapping = TextWrapping.Wrap });
            var choices = new List<CheckBox>();
            foreach (var file in PiJson.Field(preview, "files").EnumerateArray())
            {
                var path = PiJson.Text(file, "path");
                var safe = PiJson.Flag(file, "safe");
                var choice = new CheckBox { Content = new TextBlock { Text = safe ? path : path + " — " + PiJson.Text(file, "reason"), TextWrapping = TextWrapping.Wrap },
                    Tag = path, IsEnabled = safe, IsChecked = safe };
                choices.Add(choice);
                panel.Children.Add(choice);
            }
            var dialog = new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = undo ? "Undo revert" : "Revert changes",
                Content = new ScrollViewer { Content = panel, MaxHeight = 500, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                PrimaryButtonText = undo ? "Restore selected" : "Revert selected", CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close, IsPrimaryButtonEnabled = choices.Any(c => c.IsChecked == true) };
            foreach (var choice in choices)
            {
                choice.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = choices.Any(c => c.IsChecked == true);
                choice.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = choices.Any(c => c.IsChecked == true);
            }
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var result = await vm.CheckpointOperationAsync("apply", summary, undo, choices.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToArray());
            var error = PiJson.Text(result, "error");
            var done = PiJson.Field(result, "done");
            await new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = error.Length > 0 ? "Restoration incomplete" : "Files restored",
                Content = $"Restored {(done.ValueKind == JsonValueKind.Array ? done.GetArrayLength() : 0)} files." + (error.Length > 0 ? "\n" + error : ""), CloseButtonText = "Close" }.ShowAsync();
        }
        catch (Exception error) { vm.ReportAttachmentError(error.Message); }
        finally { checkpointDialogOpen = false; }
    }
}
