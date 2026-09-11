using PiAgentGui.ViewModels.Extensions;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Views;

public sealed partial class GettingStartedCard : UserControl
{
    private bool dialogOpen;
    public event EventHandler? ProvidersRequested;
    public GettingStartedCard() => InitializeComponent();
    private void OnProviders(object sender, RoutedEventArgs args) => ProvidersRequested?.Invoke(this, EventArgs.Empty);
    private async void OnSetup(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || sender is not FrameworkElement { DataContext: ExtensionCardViewModel card }) return;
        dialogOpen = true;
        try
        {
            await new ExtensionSetupDialog(card.Definition) { XamlRoot = XamlRoot }.ShowAsync();
            await card.RefreshCommand.ExecuteAsync();
        }
        catch (Exception) { if (DataContext is GettingStartedViewModel model) model.ReportError("Couldn't open extension setup. Close any other dialog and try again."); }
        finally { dialogOpen = false; }
    }
}
