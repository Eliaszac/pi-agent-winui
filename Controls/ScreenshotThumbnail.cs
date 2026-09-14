using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PiAgentGui.Models.Conversations;
using Windows.Storage.Streams;
using Windows.System;

namespace PiAgentGui.Controls;

/// <summary>A compact attachment that opens a native, image-only modal.</summary>
public sealed class ScreenshotThumbnail : ActionButton
{
    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ChatImage),
        typeof(ScreenshotThumbnail), new PropertyMetadata(null, (sender, args) =>
            ((ScreenshotThumbnail)sender).preview.Image = args.NewValue as ChatImage));

    public ChatImage? Image { get => (ChatImage?)GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    private readonly ScreenshotPreview preview = new() { Width = 112, Height = 80 };
    private ScreenshotGallery? dialog;
    private bool opening;

    public ScreenshotThumbnail()
    {
        Content = preview;
        Width = 112;
        Height = 80;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        HorizontalAlignment = HorizontalAlignment.Right;
        AutomationProperties.SetName(this, "Open attached screenshot");
        ToolTipService.SetToolTip(this, "Open screenshot");
        Click += OpenScreenshot;
        Unloaded += (_, _) => ClosePreview();
    }

    private void ClosePreview() => dialog?.Close();

    private async void OpenScreenshot(object sender, RoutedEventArgs args)
    {
        if (opening || Image is not { } image || XamlRoot is not { } root) return;
        opening = true;
        try
        {
            DependencyObject? parent = this;
            while (parent is not null && parent is not Views.ConversationView) parent = VisualTreeHelper.GetParent(parent);
            var images = parent is Views.ConversationView { ViewModel: { } conversation }
                ? conversation.Entries.SelectMany(entry => entry.Images).ToList() : new List<ChatImage>();
            var index = images.FindIndex(candidate => ReferenceEquals(candidate, image));
            if (index < 0) index = images.FindIndex(candidate => candidate == image);
            if (index < 0) { images = [image]; index = 0; }
            dialog = new ScreenshotGallery(images, index);
            await dialog.ShowAsync(root);
        }
        catch (Exception)
        {
            ToolTipService.SetToolTip(this, "Couldn't open screenshot. Try again.");
        }
        finally
        {
            dialog?.Close();
            dialog = null;
            opening = false;
            if (IsLoaded) Focus(FocusState.Programmatic);
        }
    }
}