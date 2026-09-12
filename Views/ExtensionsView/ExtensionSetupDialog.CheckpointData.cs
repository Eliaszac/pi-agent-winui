using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ExtensionSetupDialog
{
    private void AddCheckpointDataManagement()
    {
        var service = CheckpointDataService.CreateDefault();
        var clearing = false;
        Closing += (_, args) => { if (clearing) args.Cancel = true; };
        Sections.Children.Add(new TextBlock { Text = "Stored checkpoint data", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        Sections.Children.Add(new TextBlock { Text = "Clear snapshots for all projects stored by your user account on the selected target. Project files, chats and historical file summaries are kept. Revert and Undo for those checkpoints become unavailable. New runs still capture when enabled.", TextWrapping = TextWrapping.Wrap });
        var targets = new ComboBox { Header = "Clear data on", DisplayMemberPath = "Label", HorizontalAlignment = HorizontalAlignment.Stretch };
        var clear = new Controls.ActionButton { Content = "Clear stored data", IsEnabled = false };
        var notice = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var confirmation = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        var warning = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var confirm = new Controls.ActionButton { Content = "Clear snapshots permanently" };
        var cancel = new Controls.ActionButton { Content = "Cancel" };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(confirm); actions.Children.Add(cancel);
        confirmation.Children.Add(warning); confirmation.Children.Add(actions);
        Sections.Children.Add(targets); Sections.Children.Add(clear); Sections.Children.Add(confirmation); Sections.Children.Add(notice);
        clear.Click += (_, _) =>
        {
            if (targets.SelectedItem is not ExecutionTarget target) return;
            warning.Text = $"Permanently clear checkpoint snapshots on {target.Label}{(target.IsLocal ? "" : " (" + target.Host + ")")}? This cannot be undone.";
            confirmation.Visibility = Visibility.Visible;
            targets.IsEnabled = clear.IsEnabled = false;
        };
        cancel.Click += (_, _) => { confirmation.Visibility = Visibility.Collapsed; targets.IsEnabled = clear.IsEnabled = true; };
        confirm.Click += async (_, _) =>
        {
            if (targets.SelectedItem is not ExecutionTarget target) return;
            clearing = true;
            confirm.IsEnabled = cancel.IsEnabled = false;
            notice.Text = "Clearing stored snapshots…";
            try
            {
                var count = await service.ClearAsync(target);
                notice.Text = $"Cleared snapshot data for {count} checkpoints. Reconnect conversations to refresh their checkpoint status.";
            }
            catch (Exception error) { notice.Text = "Could not finish clearing data: " + error.Message; }
            finally
            {
                confirmation.Visibility = Visibility.Collapsed;
                clearing = false;
                targets.IsEnabled = clear.IsEnabled = confirm.IsEnabled = cancel.IsEnabled = true;
            }
        };
        Opened += async (_, _) =>
        {
            try { targets.ItemsSource = await service.GetTargetsAsync(); targets.SelectedIndex = 0; clear.IsEnabled = true; }
            catch (Exception error) { notice.Text = "Could not load targets: " + error.Message; }
        };
    }
}
