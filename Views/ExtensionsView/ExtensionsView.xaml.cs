using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Views;

public sealed partial class ExtensionsView : UserControl
{
    private bool dialogOpen;
    private bool resizing;
    public ExtensionsView() => InitializeComponent();
    private void OnCardsLoaded(object sender, RoutedEventArgs args) => ResizeCards();
    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs args) => ResizeCards();
    private void OnCardLoaded(object sender, RoutedEventArgs args) => ResizeCards();
    private void OnCardContentSizeChanged(object sender, SizeChangedEventArgs args) => ResizeCards();
    private void ResizeCards()
    {
        if (resizing || ExtensionCards.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        resizing = true;
        try
        {
            var width = Math.Max(0, ExtensionCards.ActualWidth - 20);
            var columns = width >= 960 ? 3 : width >= 640 ? 2 : 1;
            panel.MaximumRowsOrColumns = columns;
            panel.ItemWidth = Math.Max(1, width / columns);
            var cardHeight = 0d;
            foreach (var item in ExtensionCards.Items)
            {
                if (ExtensionCards.ContainerFromItem(item) is not GridViewItem { ContentTemplateRoot: FrameworkElement root }) continue;
                root.Measure(new Windows.Foundation.Size(panel.ItemWidth, double.PositiveInfinity));
                cardHeight = Math.Max(cardHeight, root.DesiredSize.Height);
            }
            if (cardHeight > 0 && panel.ItemHeight != Math.Ceiling(cardHeight)) panel.ItemHeight = Math.Ceiling(cardHeight);
        }
        finally { resizing = false; }
    }
    private async void OnSetupClicked(object sender, RoutedEventArgs args) => await ShowExtensionAsync(sender, false);
    private async void OnDetailsClicked(object sender, RoutedEventArgs args) => await ShowExtensionAsync(sender, true);
    private async Task ShowExtensionAsync(object sender, bool details)
    {
        if (dialogOpen || sender is not FrameworkElement { DataContext: ExtensionCardViewModel card }) return;
        dialogOpen = true;
        try { await new ExtensionSetupDialog(card.Definition, details) { XamlRoot = XamlRoot }.ShowAsync(); }
        finally { dialogOpen = false; }
    }
}
