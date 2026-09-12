namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private readonly DispatcherTimer dockerTimer = new() { Interval = TimeSpan.FromSeconds(10) };

    private void InitializeDockerPanel()
    {
        dockerTimer.Tick += async (_, _) => await Docker.RefreshAsync(clearMessage: false);
        Docker.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(Docker.IsOpen))
            {
                if (Docker.IsOpen) { dockerTimer.Start(); _ = Docker.RefreshAsync(); }
                else { dockerTimer.Stop(); PanelHidden("docker"); }
            }
            if (change.PropertyName == nameof(Docker.Enabled))
            {
                if (!Docker.Enabled)
                    foreach (var state in sidePanels.Values)
                        foreach (var tab in state.Tabs.Where(tab => tab.Kind == "docker").ToArray()) state.Close(tab);
                ApplySidePanel();
            }
        };
        ViewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName is nameof(ViewModel.Chat) or nameof(ViewModel.SelectedProject)) Docker.SelectProject(ViewModel.SelectedProject?.Project.Id);
        };
        Unloaded += (_, _) => { dockerTimer.Stop(); Docker.Dispose(); };
    }

    private void OnDockerClicked(object sender, RoutedEventArgs args) => OpenSidePanel("docker");
}
