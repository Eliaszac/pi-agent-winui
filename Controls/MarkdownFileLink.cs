using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using PiAgentGui.Services.Files;
using PiAgentGui.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Text;

namespace PiAgentGui.Controls;

/// <summary>Resolves a file mention asynchronously and keeps all actions bound to its original conversation target.</summary>
internal sealed class MarkdownFileLink : ActionHyperlinkButton
{
    private readonly WorkspaceFileLinks files;
    private readonly FileMention mention;
    private readonly TextBlock label;
    private CancellationTokenSource? lifetime;
    internal event Action<Brush>? MissingFileResolved;

    internal MarkdownFileLink(WorkspaceFileLinks files, FileMention mention, RichTextBlock owner)
    {
        this.files = files;
        this.mention = mention;
        label = new TextBlock { Text = mention.Text, TextWrapping = TextWrapping.Wrap };
        Content = label;
        FontFamily = new FontFamily("Consolas");
        FontSize = 14 * ReadingPreferences.Scale;
        Padding = new Thickness(0);
        MinHeight = MinWidth = 0;
        MaxWidth = owner.MaxWidth;
        void Resize(object sender, SizeChangedEventArgs args) { if (args.NewSize.Width > 0) MaxWidth = args.NewSize.Width; }
        Loaded += (_, _) => { owner.SizeChanged += Resize; if (owner.ActualWidth > 0) MaxWidth = owner.ActualWidth; };
        Unloaded += (_, _) => owner.SizeChanged -= Resize;
        IsEnabled = false;
        AutomationProperties.SetName(this, mention.Text);
        ToolTipService.SetToolTip(this, "Looking up file · " + files.Target.Label);
        Loaded += OnLoaded;
        Unloaded += (_, _) => { lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; };
        Click += async (_, _) => await ActivateAsync(copy: false);
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Open in editor", Icon = new FontIcon { Glyph = "\uE8A7" } };
        var copy = new MenuFlyoutItem { Text = "Copy path", Icon = new SymbolIcon(Symbol.Copy) };
        open.Click += async (_, _) => await ActivateAsync(copy: false);
        copy.Click += async (_, _) => await ActivateAsync(copy: true);
        menu.Items.Add(open); menu.Items.Add(copy);
        ContextFlyout = menu;
        ContextRequested += (_, args) =>
        {
            args.Handled = true;
            if (args.TryGetPosition(this, out var position)) menu.ShowAt(this, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = position });
            else menu.ShowAt(this);
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        lifetime?.Cancel(); lifetime?.Dispose();
        lifetime = new CancellationTokenSource();
        IsEnabled = false;
        label.TextDecorations = TextDecorations.None;
        var token = lifetime.Token;
        try
        {
            var matches = await files.ResolveAsync(mention, token);
            token.ThrowIfCancellationRequested();
            IsEnabled = matches.Count > 0;
            label.TextDecorations = IsEnabled ? TextDecorations.Underline : TextDecorations.None;
            ToolTipService.SetToolTip(this, matches.Count switch
            {
                0 => "No matching workspace file · " + files.Target.Label,
                1 => "Open in editor · " + files.FullPath(matches[0]),
                _ => $"Choose among {matches.Count} files · {files.Target.Label}"
            });
            if (matches.Count == 0) MissingFileResolved?.Invoke(label.Foreground);
        }
        catch (OperationCanceledException) { if (!token.IsCancellationRequested) ToolTipService.SetToolTip(this, "File lookup timed out."); }
        catch (Exception) { if (!token.IsCancellationRequested) ToolTipService.SetToolTip(this, "File lookup unavailable. Check the workspace connection or permissions."); }
    }

    private async Task ActivateAsync(bool copy)
    {
        if (lifetime is null) return;
        var token = lifetime.Token;
        try
        {
            var matches = await files.ResolveAsync(mention, token, refresh: true);
            token.ThrowIfCancellationRequested();
            if (matches.Count == 0) { ShowStatus("The file was not found in this workspace."); return; }
            if (matches.Count == 1) { await ExecuteAsync(matches[0], copy, token); return; }
            var picker = new ListView { ItemsSource = matches, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None, MaxHeight = 320, MinWidth = 240, MaxWidth = 560 };
            AutomationProperties.SetName(picker, copy ? "Choose a file path to copy" : "Choose a file to open");
            var flyout = new Flyout { Content = picker };
            picker.ItemClick += async (_, args) =>
            {
                flyout.Hide();
                try { await ExecuteAsync((string)args.ClickedItem, copy, token); }
                catch (OperationCanceledException) { }
                catch (Exception exception) { if (!token.IsCancellationRequested) ShowStatus(exception.Message); }
            };
            flyout.ShowAt(this);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (!token.IsCancellationRequested) ShowStatus(exception.Message); }
    }

    private async Task ExecuteAsync(string path, bool copy, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (copy)
        {
            var data = new DataPackage(); data.SetText(files.FullPath(path)); Clipboard.SetContent(data);
            ShowStatus("Path copied.");
        }
        else await files.OpenAsync(path, mention, token);
    }

    private void ShowStatus(string message)
    {
        if (IsLoaded) new Flyout { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 } }.ShowAt(this);
    }
}
