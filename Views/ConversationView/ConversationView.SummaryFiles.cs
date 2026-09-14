using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private async void OnSummaryFileClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: ChangedFileViewModel file } button || ViewModel?.FileLinks is not { } links) return;
        button.IsEnabled = false;
        try { await links.OpenExactAsync(file.OpenPath, CancellationToken.None); }
        catch (Exception error)
        {
            if (button.IsLoaded)
                new Flyout { Content = new TextBlock { Text = error.Message, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 } }.ShowAt(button);
        }
        finally { button.IsEnabled = true; }
    }
}
