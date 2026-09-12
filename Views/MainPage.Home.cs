using PiAgentGui.ViewModels.Home;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    public HomeViewModel Home { get; }
    private readonly DispatcherTimer homeTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private void InitializeHome()
    {
        HomePane.DataContext = Home;
        HomePane.NewConversationRequested += (sender, _) => ShowHomeProjects(sender, create: true);
        HomePane.OpenProjectRequested += (sender, _) => ShowHomeProjects(sender, create: false);
        HomePane.AddProjectRequested += (sender, _) => OnNewProjectClicked(sender!, new());
        homeTimer.Tick += async (_, _) => await Home.RefreshCommand.ExecuteAsync();
        ViewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName != nameof(ViewModel.ShowHome)) return;
            if (ViewModel.ShowHome) homeTimer.Start(); else homeTimer.Stop();
        };
        if (ViewModel.ShowHome) homeTimer.Start();
        Unloaded += (_, _) => { homeTimer.Stop(); Home.Dispose(); };
    }

    private void OnHomeClicked(object sender, RoutedEventArgs args) => ViewModel.OpenHome();

    private void ShowHomeProjects(object? sender, bool create)
    {
        if (sender is not FrameworkElement anchor) return;
        var menu = new MenuFlyout();
        foreach (var project in ViewModel.Projects)
        {
            var item = new Controls.ActionMenuFlyoutItem { Text = project.Name };
            item.Click += async (_, _) =>
            {
                if (!ViewModel.Projects.Contains(project)) return;
                if (create) await ViewModel.NewConversationCommand.ExecuteAsync(project);
                else ViewModel.SelectProject(project);
            };
            menu.Items.Add(item);
        }
        menu.ShowAt(anchor);
    }
}
