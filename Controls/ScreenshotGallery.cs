using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PiAgentGui.Models.Conversations;
using Windows.Storage.Streams;
using Windows.System;

namespace PiAgentGui.Controls;

/// <summary>A conversation screenshot viewer with native light dismissal and keyboard navigation.</summary>
internal sealed class ScreenshotGallery
{
    private readonly Popup popup = new() { IsLightDismissEnabled = true, LightDismissOverlayMode = LightDismissOverlayMode.On };
    private readonly Grid layout = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly Image display = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock count = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock error = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly ActionButton previous;
    private readonly ActionButton next;
    private readonly ActionButton close;
    private readonly IReadOnlyList<ChatImage> images;
    private int index;
    private int revision;

    public ScreenshotGallery(IReadOnlyList<ChatImage> images, int index)
    {
        this.images = images;
        this.index = index;
        layout.RowDefinitions.Add(new() { Height = new GridLength(40) });
        layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(40) });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(40) });
        close = Icon("\uE711", "Close screenshot (Escape)", Close);
        previous = Icon("\uE76B", "Previous screenshot (Left arrow)", () => Move(-1));
        next = Icon("\uE76C", "Next screenshot (Right arrow)", () => Move(1));
        Grid.SetColumn(close, 2); Grid.SetColumn(count, 1);
        Grid.SetRow(previous, 1); Grid.SetRow(next, 1); Grid.SetColumn(next, 2);
        var imageArea = new ActionButton { Content = display, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetName(imageArea, "Screenshot. Click to close");
        imageArea.Click += (_, _) => Close();
        Grid.SetRow(imageArea, 1); Grid.SetColumn(imageArea, 1);
        Grid.SetRow(error, 1); Grid.SetColumn(error, 1);
        error.IsHitTestVisible = false;
        layout.Children.Add(imageArea); layout.Children.Add(error); layout.Children.Add(count);
        layout.Children.Add(previous); layout.Children.Add(next); layout.Children.Add(close);
        layout.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        layout.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        layout.PointerPressed += (_, args) => { if (ReferenceEquals(args.OriginalSource, layout)) { args.Handled = true; Close(); } };
        AutomationProperties.SetLiveSetting(count, AutomationLiveSetting.Polite);
        popup.Child = layout;
        popup.Closed += (_, _) => { revision++; display.Source = null; };
    }

    private static ActionButton Icon(string glyph, string name, Action action)
    {
        var button = new ActionButton { Content = new FontIcon { Glyph = glyph, FontSize = 14 }, Width = 32, Height = 32,
            Padding = new Thickness(6), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetName(button, name); ToolTipService.SetToolTip(button, name);
        button.Click += (_, _) => action();
        return button;
    }

    public async Task ShowAsync(XamlRoot root)
    {
        popup.XamlRoot = root;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Closed(object? sender, object args) => completion.TrySetResult();
        void Resize(XamlRoot sender, XamlRootChangedEventArgs args) => Size(root);
        popup.Closed += Closed;
        root.Changed += Resize;
        try
        {
            Size(root); popup.IsOpen = true; close.Focus(FocusState.Programmatic);
            _ = LoadAsync();
            await completion.Task;
        }
        finally { root.Changed -= Resize; popup.Closed -= Closed; popup.IsOpen = false; popup.Child = null; }
    }

    private void Size(XamlRoot root)
    {
        layout.Width = Math.Max(1, root.Size.Width - 32);
        layout.Height = Math.Max(1, root.Size.Height - 32);
        popup.HorizontalOffset = (root.Size.Width - layout.Width) / 2;
        popup.VerticalOffset = (root.Size.Height - layout.Height) / 2;
    }

    public void Close() => popup.IsOpen = false;
    private void Move(int direction)
    {
        var selected = Math.Clamp(index + direction, 0, images.Count - 1);
        if (selected == index) return;
        index = selected; _ = LoadAsync();
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape) Close();
        else if (args.Key == VirtualKey.Left) Move(-1);
        else if (args.Key == VirtualKey.Right) Move(1);
        else return;
        args.Handled = true;
    }
    private async Task LoadAsync()
    {
        var current = ++revision;
        previous.IsEnabled = index > 0; next.IsEnabled = index < images.Count - 1;
        previous.Visibility = next.Visibility = images.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        count.Text = $"{index + 1} / {images.Count}";
        display.Source = null; error.Text = "";
        try
        {
            var bytes = Convert.FromBase64String(images[index].Data);
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = (int)Math.Clamp(Math.Ceiling(layout.Width * popup.XamlRoot.RasterizationScale), 1, 4096) };
            await bitmap.SetSourceAsync(stream);
            if (revision == current && popup.IsOpen) display.Source = bitmap;
        }
        catch (Exception) { if (revision == current && popup.IsOpen) error.Text = "This screenshot could not be loaded."; }
    }
}
