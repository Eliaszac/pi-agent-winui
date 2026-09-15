using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ArtifactsPanel : UserControl
{
    public ArtifactsPanel() => InitializeComponent();
    private async void OnRefresh(object sender, RoutedEventArgs args)
    {
        if (DataContext is ArtifactPanelViewModel model) await model.RefreshAsync();
    }
}
