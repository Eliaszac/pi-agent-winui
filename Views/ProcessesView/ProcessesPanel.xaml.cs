using PiAgentGui.ViewModels.Processes;
using PiAgentGui.Models.Pi;
using Microsoft.UI.Xaml.Input;

namespace PiAgentGui.Views;

public sealed partial class ProcessesPanel : UserControl
{
    public ProcessesPanel() => InitializeComponent();
    private bool confirming;
    private void OnRowEntered(object sender, PointerRoutedEventArgs args) => ((Grid)sender).Children[1].Opacity = 1;
    private void OnRowExited(object sender, PointerRoutedEventArgs args)
    {
        var button = (Controls.ActionButton)((Grid)sender).Children[1];
        if (button.FocusState == FocusState.Unfocused) button.Opacity = 0;
    }
    private void OnRowFocused(object sender, RoutedEventArgs args) => ((Grid)sender).Children[1].Opacity = 1;
    private void OnRowUnfocused(object sender, RoutedEventArgs args) => ((Grid)sender).Children[1].Opacity = 0;

    private async void OnStop(object sender, RoutedEventArgs args)
    {
        if (confirming || DataContext is not ProcessesPanelViewModel model || !model.CanStop
            || sender is not FrameworkElement { Tag: AgentProcess process }) return;
        confirming = true;
        try
        {
            var dialog = new Controls.ActionContentDialog
            {
                XamlRoot = XamlRoot, Title = $"Stop {process.Name}?",
                Content = $"This will terminate PID {process.Identity.Id} and its child processes. An active agent command may report a failure.",
                PrimaryButtonText = "Stop", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) await model.StopAsync(process);
        }
        catch (Exception) { model.ReportError("Couldn't open the stop confirmation. Close any other dialog and try again."); }
        finally { confirming = false; }
    }
    private void OnClose(object sender, RoutedEventArgs args)
    {
        if (DataContext is ProcessesPanelViewModel model) model.IsOpen = false;
    }
}
