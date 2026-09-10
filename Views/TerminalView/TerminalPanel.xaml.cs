using System.Collections.Specialized;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.Views;

public sealed partial class TerminalPanel : UserControl
{
    private TerminalPanelViewModel? model;
    private readonly Dictionary<TerminalTabViewModel, TabViewItem> tabs = [];
    private readonly Dictionary<TerminalTabViewModel, TerminalSurface> displays = [];
    public Func<string?>? CurrentDirectory { get; set; }

    public TerminalPanel() => InitializeComponent();

    public void Bind(TerminalPanelViewModel viewModel)
    {
        model = viewModel;
        model.Tabs.CollectionChanged += OnTabsChanged;
        model.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(TerminalPanelViewModel.Selected))
                TerminalTabs.SelectedItem = model.Selected is { } selected && tabs.TryGetValue(selected, out var tab) ? tab : null;
            if (change.PropertyName == nameof(TerminalPanelViewModel.IsOpen) && model.IsOpen) FocusSelected();
        };
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
            foreach (TerminalTabViewModel removed in args.OldItems)
                if (tabs.Remove(removed, out var tab))
                {
                    if (displays.Remove(removed, out var surface)) { surface.Dispose(); TerminalDisplays.Children.Remove(surface); }
                    TerminalTabs.TabItems.Remove(tab);
                }
        if (args.NewItems is not null)
            foreach (TerminalTabViewModel added in args.NewItems)
            {
                var surface = new TerminalSurface(added.Session) { Visibility = Visibility.Collapsed };
                displays[added] = surface;
                TerminalDisplays.Children.Add(surface);
                var item = new TabViewItem { Header = added.Title, Tag = added, IsClosable = true };
                tabs[added] = item;
                TerminalTabs.TabItems.Add(item);
            }
    }

    private void OnAddTabClicked(TabView sender, object args)
    {
        if (CurrentDirectory?.Invoke() is { } directory) model?.Add(directory);
    }
    private async void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (model is not null && args.Tab.Tag is TerminalTabViewModel tab)
        {
            try { await model.CloseAsync(tab); }
            catch (Exception)
            {
                var dialog = new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = "Couldn't close the terminal",
                    Content = "The shell could not be fully stopped. Close the app to retry cleanup.", CloseButtonText = "Close" };
                await dialog.ShowAsync();
            }
        }
    }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (model is not null && TerminalTabs.SelectedItem is TabViewItem { Tag: TerminalTabViewModel tab }) model.Selected = tab;
        FocusSelected();
    }
    private void OnHideClicked(object sender, RoutedEventArgs args) => model?.Hide();
    private void FocusSelected() => DispatcherQueue.TryEnqueue(() =>
    {
        var selected = (TerminalTabs.SelectedItem as TabViewItem)?.Tag as TerminalTabViewModel;
        foreach (var (tab, surface) in displays)
            surface.Visibility = tab == selected ? Visibility.Visible : Visibility.Collapsed;
        if (selected is not null && displays.TryGetValue(selected, out var active)) active.FocusTerminal();
    });
    public void CloseDisplays()
    {
        foreach (var surface in displays.Values) surface.Dispose();
    }
}
