namespace PiAgentGui.Views;

public sealed partial class HomeView : UserControl
{
    public string VersionLabel => $"{Configuration.ApplicationIdentity.Name} · v{Configuration.ApplicationIdentity.Version}";
    public event EventHandler? NewConversationRequested;
    public event EventHandler? OpenProjectRequested;
    public event EventHandler? AddProjectRequested;
    public HomeView() => InitializeComponent();
    private void OnNewConversation(object sender, RoutedEventArgs args) => NewConversationRequested?.Invoke(sender, EventArgs.Empty);
    private void OnOpenProject(object sender, RoutedEventArgs args) => OpenProjectRequested?.Invoke(sender, EventArgs.Empty);
    private void OnAddProject(object sender, RoutedEventArgs args) => AddProjectRequested?.Invoke(sender, EventArgs.Empty);
    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs args) => HomeViewport.MinHeight = args.NewSize.Height;
    private void OnContentSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var narrow = args.NewSize.Width < 620;
        Grid.SetColumn(ProjectBreakdown, narrow ? 0 : 1);
        Grid.SetRow(ProjectBreakdown, narrow ? 1 : 0);
        Grid.SetColumnSpan(ModelBreakdown, narrow ? 2 : 1);
        Grid.SetColumnSpan(ProjectBreakdown, narrow ? 2 : 1);
    }
}
