using PiAgentGui.ViewModels.Docker;

namespace PiAgentGui.Views;

public sealed partial class DockerPanel : UserControl
{
    private bool managing;
    public DockerPanel() => InitializeComponent();
    private async void OnManage(object sender, RoutedEventArgs args)
    {
        if (managing || DataContext is not DockerPanelViewModel model) return;
        managing = true;
        try { await new ExtensionSetupDialog(ViewModels.Extensions.SupportedExtensions.Docker(model)) { XamlRoot = XamlRoot }.ShowAsync(); }
        catch (Exception error) { model.ReportError(error); }
        finally { managing = false; }
    }
}
