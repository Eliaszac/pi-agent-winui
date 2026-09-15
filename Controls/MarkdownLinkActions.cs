using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Text;

namespace PiAgentGui.Controls;

/// <summary>Creates native inline link controls with their own accessible context menu.</summary>
internal static class MarkdownLinkActions
{
    internal static ActionHyperlinkButton Create(Uri uri, TextBlock label, RichTextBlock owner)
    {
        label.IsTextSelectionEnabled = false;
        label.TextWrapping = TextWrapping.Wrap;
        label.TextDecorations = TextDecorations.Underline;
        var link = new ActionHyperlinkButton
        {
            NavigateUri = uri,
            Content = label,
            Padding = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            MaxWidth = owner.MaxWidth,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        link.SetBinding(Control.FontSizeProperty, new Binding { Source = owner, Path = new PropertyPath("FontSize"), Mode = BindingMode.OneWay });
        ToolTipService.SetToolTip(link, uri.AbsoluteUri);
        AutomationProperties.SetHelpText(link, uri.AbsoluteUri);

        void Resize(object sender, SizeChangedEventArgs args)
        {
            if (args.NewSize.Width > 0) link.MaxWidth = args.NewSize.Width;
        }
        link.Loaded += (_, _) =>
        {
            owner.SizeChanged += Resize;
            if (owner.ActualWidth > 0) link.MaxWidth = owner.ActualWidth;
        };
        link.Unloaded += (_, _) => owner.SizeChanged -= Resize;

        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Open in external browser", Icon = new FontIcon { Glyph = "\uE8A7" } };
        var copy = new MenuFlyoutItem { Text = "Copy link", Icon = new SymbolIcon(Symbol.Copy) };
        open.Click += async (_, _) =>
        {
            try
            {
                if (!await Launcher.LaunchUriAsync(uri)) ShowError(link, "Couldn't open the link. Check your default browser.");
            }
            catch (Exception) { ShowError(link, "Couldn't open the link. Check your default browser."); }
        };
        copy.Click += (_, _) =>
        {
            try
            {
                var data = new DataPackage();
                data.SetText(uri.AbsoluteUri);
                Clipboard.SetContent(data);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                ShowError(link, "Couldn't copy the link. Try again.");
            }
        };
        menu.Items.Add(open);
        if (uri.Scheme is "http" or "https")
        {
            var embedded = new MenuFlyoutItem { Text = "Open in embedded browser", Icon = new FontIcon { Glyph = "\uE774" } };
            embedded.Click += async (_, _) =>
            {
                DependencyObject? parent = link;
                while (parent is not null && parent is not Views.ConversationView) parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
                if (parent is Views.ConversationView { ViewModel.OpenBrowser: { } launch })
                {
                    try { await launch(uri); } catch (Exception error) { ShowError(link, error.Message); }
                }
                else ShowError(link, "Open a conversation to use the embedded browser.");
            };
            menu.Items.Add(embedded);
        }
        menu.Items.Add(copy);
        link.ContextFlyout = menu;
        // Handle the request at the embedded control, before RichTextBlock can
        // substitute its selection flyout. ContextRequested also covers keyboard input.
        link.ContextRequested += (_, args) =>
        {
            args.Handled = true;
            if (args.TryGetPosition(link, out var position))
                menu.ShowAt(link, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = position });
            else menu.ShowAt(link);
        };
        return link;
    }

    private static void ShowError(FrameworkElement link, string message)
    {
        if (!link.IsLoaded) return;
        var flyout = new Flyout { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 280 } };
        flyout.ShowAt(link);
    }
}
