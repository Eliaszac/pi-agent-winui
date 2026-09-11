namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private bool errorDetailsOpen;
    private async void OnErrorDetails(object sender, RoutedEventArgs args)
    {
        if (errorDetailsOpen || ViewModel is not { } model) return;
        errorDetailsOpen = true;
        try { await new ErrorDetailsDialog(model.ErrorTitle, model.ErrorHelp, model.ErrorDiagnosticReport) { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception)
        {
            // A second dialog may already be open. The original error remains visible and selectable.
        }
        finally { errorDetailsOpen = false; }
    }
}
