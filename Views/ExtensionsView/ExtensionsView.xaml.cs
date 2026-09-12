using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Views;

public sealed partial class ExtensionsView : UserControl
{
    private bool dialogOpen;
    private bool resizing;
    public ExtensionsView() => InitializeComponent();
    private void OnCardsLoaded(object sender, RoutedEventArgs args) => ResizeCards(sender);
    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs args)
    {
        // Expander animation changes height; only width changes require a new card layout.
        if (Math.Abs(args.NewSize.Width - args.PreviousSize.Width) > 0.5) ResizeCards(sender);
    }
    private void OnCardLoaded(object sender, RoutedEventArgs args) => ResizeCards(sender);
    private void OnCardContentSizeChanged(object sender, SizeChangedEventArgs args) => ResizeCards(sender);
    private void ResizeCards(object sender)
    {
        var ancestor = sender as DependencyObject;
        while (ancestor is not null && ancestor is not GridView)
            ancestor = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(ancestor);
        if (resizing || ancestor is not GridView cards || cards.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        resizing = true;
        try
        {
            var width = Math.Max(0, cards.ActualWidth - 20);
            var columns = width >= 960 ? 3 : width >= 640 ? 2 : 1;
            if (panel.MaximumRowsOrColumns != columns) panel.MaximumRowsOrColumns = columns;
            var itemWidth = Math.Max(1, width / columns);
            if (Math.Abs(panel.ItemWidth - itemWidth) > 0.5 || double.IsNaN(panel.ItemWidth)) panel.ItemWidth = itemWidth;
            var cardHeight = 0d;
            foreach (var item in cards.Items)
            {
                if (cards.ContainerFromItem(item) is not GridViewItem { ContentTemplateRoot: FrameworkElement root }) continue;
                root.Measure(new Windows.Foundation.Size(panel.ItemWidth, double.PositiveInfinity));
                cardHeight = Math.Max(cardHeight, root.DesiredSize.Height);
            }
            if (cardHeight > 0 && panel.ItemHeight != Math.Ceiling(cardHeight)) panel.ItemHeight = Math.Ceiling(cardHeight);
        }
        finally { resizing = false; }
    }
    private async void OnSetupClicked(object sender, RoutedEventArgs args) => await ShowExtensionAsync(sender, false);
    private async void OnExtensionToggled(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleSwitch { DataContext: ExtensionCardViewModel card } toggle && toggle.IsOn != card.IsEnabled)
            await card.SetEnabledAsync(toggle.IsOn);
    }
    private async void OnDetailsClicked(object sender, RoutedEventArgs args) => await ShowExtensionAsync(sender, true);
    private async Task ShowExtensionAsync(object sender, bool details)
    {
        if (dialogOpen || sender is not FrameworkElement { DataContext: ExtensionCardViewModel card }) return;
        dialogOpen = true;
        try { await new ExtensionSetupDialog(card.Definition, details) { XamlRoot = XamlRoot }.ShowAsync(); }
        finally { dialogOpen = false; await card.RefreshCommand.ExecuteAsync(); }
    }
}

