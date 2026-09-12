using PiAgentGui.Controls;
using PiAgentGui.ViewModels.Providers;

namespace PiAgentGui.Views;

public sealed partial class ProvidersView : UserControl
{
    private bool resizing;
    private bool dialogOpen;
    public ProvidersView() => InitializeComponent();
    private void OnCardsLoaded(object sender, RoutedEventArgs args) => ResizeCards();
    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs args) => ResizeCards();
    private void ResizeCards()
    {
        if (resizing || ProviderCards.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        resizing = true;
        try
        {
            var width = Math.Max(1, ProviderCards.ActualWidth - 20);
            var columns = width >= 960 ? 3 : width >= 640 ? 2 : 1;
            panel.MaximumRowsOrColumns = columns;
            panel.ItemWidth = width / columns;
            var height = 0d;
            foreach (var item in ProviderCards.Items)
            {
                if (ProviderCards.ContainerFromItem(item) is not GridViewItem { ContentTemplateRoot: FrameworkElement root }) continue;
                root.Measure(new Windows.Foundation.Size(panel.ItemWidth, double.PositiveInfinity));
                height = Math.Max(height, root.DesiredSize.Height);
            }
            if (height > 0) panel.ItemHeight = Math.Ceiling(height);
        }
        finally { resizing = false; }
    }
    private async void OnRefreshClicked(object sender, RoutedEventArgs args)
    {
        if (DataContext is ProvidersViewModel vm) await vm.RefreshAsync();
    }
    private async void OnDetailsClicked(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || sender is not FrameworkElement { DataContext: ProviderCardViewModel card }) return;
        dialogOpen = true;
        try
        {
            await new ActionContentDialog { XamlRoot = XamlRoot, Title = card.Name, CloseButtonText = "Close",
                Content = new ScrollViewer { MaxHeight = 440, Content = new TextBlock { Text = card.ModelDetails,
                    TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } } }.ShowAsync();
        }
        finally { dialogOpen = false; }
    }
    private async void OnSetupClicked(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || DataContext is not ProvidersViewModel { CanInteract: true } vm || sender is not FrameworkElement { DataContext: ProviderCardViewModel card }) return;
        dialogOpen = true;
        try
        {
            if (card.IsOllama && vm.Ollama is { } ollama)
                await new OllamaSetupDialog(ollama) { XamlRoot = XamlRoot }.ShowAsync();
            else await new ProviderSetupDialog(vm, card) { XamlRoot = XamlRoot }.ShowAsync();
        }
        finally { dialogOpen = false; }
    }
}
