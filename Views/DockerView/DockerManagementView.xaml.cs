using PiAgentGui.Models.Docker;
using PiAgentGui.ViewModels.Docker;

namespace PiAgentGui.Views;

public sealed partial class DockerManagementView : UserControl
{
    public DockerManagementView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { if (DataContext is DockerPanelViewModel model) await model.LoadManagementAsync(); };
    }

    private async void OnRefresh(object sender, RoutedEventArgs args)
    { if (DataContext is DockerPanelViewModel model) await model.LoadManagementAsync(); }

    private async void OnAddSsh(object sender, RoutedEventArgs args)
    {
        if (DataContext is not DockerPanelViewModel model || SshTargets.SelectedItem is not DockerSource source) return;
        try { await model.AddSourceAsync(source); }
        catch (Exception error) { model.ReportError(error); }
    }

    private async void OnRemove(object sender, RoutedEventArgs args)
    {
        if (DataContext is not DockerPanelViewModel model || sender is not Button { DataContext: DockerSourceViewModel source } button) return;
        button.IsEnabled = false;
        try { await model.RemoveSourceAsync(source.Source); }
        catch (Exception error) { model.ReportError(error); }
        finally { button.IsEnabled = true; }
    }

    private void OnProjects(object sender, RoutedEventArgs args)
    {
        if (DataContext is not DockerPanelViewModel model || sender is not FrameworkElement { DataContext: DockerContainerViewModel row } anchor) return;
        var content = new StackPanel { Spacing = 8, MinWidth = 220, MaxWidth = 360 };
        content.Children.Add(new TextBlock { Text = "Show in projects", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var checkboxes = model.Projects.Select(project => new CheckBox { Content = project.Name, Tag = project.Id, IsChecked = model.LinkedProjects(row.Container).Contains(project.Id) }).ToArray();
        var choices = new StackPanel();
        foreach (var checkbox in checkboxes) choices.Children.Add(checkbox);
        content.Children.Add(new ScrollViewer { Content = choices, MaxHeight = 260, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var all = new Controls.ActionButton { Content = "Clear selection · all projects" };
        all.Click += (_, _) => { foreach (var checkbox in checkboxes) checkbox.IsChecked = false; };
        content.Children.Add(all);
        var save = new Controls.ActionButton { Content = "Save", HorizontalAlignment = HorizontalAlignment.Right };
        var flyout = new Flyout { Content = content };
        save.Click += async (_, _) =>
        {
            save.IsEnabled = false;
            try { await model.LinkAsync(row.Container, checkboxes.Where(box => box.IsChecked == true).Select(box => (Guid)box.Tag)); flyout.Hide(); }
            catch (Exception error) { model.ReportError(error); }
            finally { save.IsEnabled = true; }
        };
        content.Children.Add(save);
        flyout.ShowAt(anchor);
    }
}
