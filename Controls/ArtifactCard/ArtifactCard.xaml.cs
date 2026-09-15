using PiAgentGui.Services.Dialogs;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Controls;

public sealed partial class ArtifactCard : UserControl
{
    private bool busy;
    public ArtifactCard() => InitializeComponent();
    private async void OnAction(object sender, RoutedEventArgs args)
    {
        if (busy || DataContext is not ArtifactItemViewModel item || sender is not FrameworkElement { Tag: string action }) return;
        busy = true;
        try { await new ArtifactActions().ExecuteAsync(XamlRoot, item, action); }
        catch (Exception error)
        {
            item.Owner.Error = error.Message;
            try { await new ActionContentDialog { XamlRoot = XamlRoot, Title = "Artifact unavailable", Content = error.Message, CloseButtonText = "Close" }.ShowAsync(); }
            catch (Exception) { /* The panel retains the error if another dialog owns the window. */ }
        }
        finally { busy = false; }
    }
}
