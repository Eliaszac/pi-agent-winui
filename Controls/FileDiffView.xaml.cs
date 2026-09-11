using PiAgentGui.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Controls;

/// <summary>Reusable, bounded unified diff preview with native line gutters and themed change backgrounds.</summary>
public sealed partial class FileDiffView : UserControl
{
    private string? renderedPatch;
    private bool rendered;
    private bool dialogOpen;
    public event EventHandler<Models.Conversations.DiffLine>? RevealContextRequested;
    private void OnRevealContext(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: Models.Conversations.DiffLine gap }) RevealContextRequested?.Invoke(this, gap);
    }
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(nameof(Document), typeof(Models.Conversations.UnifiedDiffDocument), typeof(FileDiffView),
        new PropertyMetadata(null, (sender, _) => { var view = (FileDiffView)sender; view.rendered = false; view.Render(); }));
    public Models.Conversations.UnifiedDiffDocument? Document { get => (Models.Conversations.UnifiedDiffDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public static readonly DependencyProperty IsFullViewProperty = DependencyProperty.Register(nameof(IsFullView), typeof(bool), typeof(FileDiffView),
        new PropertyMetadata(false, (sender, _) => { var view = (FileDiffView)sender; view.rendered = false; view.Render(); }));
    public bool IsFullView { get => (bool)GetValue(IsFullViewProperty); set => SetValue(IsFullViewProperty, value); }
    public static readonly DependencyProperty PatchProperty = DependencyProperty.Register(nameof(Patch), typeof(string), typeof(FileDiffView),
        new PropertyMetadata(null, (sender, _) => ((FileDiffView)sender).Render()));
    public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(nameof(FileName), typeof(string), typeof(FileDiffView),
        new PropertyMetadata(null, (sender, _) => { var view = (FileDiffView)sender; view.rendered = false; view.Render(); }));
    public static readonly DependencyProperty ShowFileNameProperty = DependencyProperty.Register(nameof(ShowFileName), typeof(bool), typeof(FileDiffView),
        new PropertyMetadata(true, (sender, _) => { var view = (FileDiffView)sender; view.rendered = false; view.Render(); }));
    public string? Patch { get => (string?)GetValue(PatchProperty); set => SetValue(PatchProperty, value); }
    public string? FileName { get => (string?)GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public bool ShowFileName { get => (bool)GetValue(ShowFileNameProperty); set => SetValue(ShowFileNameProperty, value); }
    public FileDiffView()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
        SizeChanged += (_, _) => ResizeViewport();
    }
    private void Render()
    {
        if (!IsLoaded || (rendered && renderedPatch == Patch)) return;
        renderedPatch = Patch; rendered = true;
        var document = Document ?? UnifiedDiffParser.Parse(Patch, IsFullView ? 20000 : UnifiedDiffParser.MaximumLines);
        Toolbar.Visibility = IsFullView ? Visibility.Collapsed : Visibility.Visible;
        FileTitle.Text = !string.IsNullOrWhiteSpace(FileName) ? FileName : document.FileName.Length > 0 ? document.FileName : "Changes";
        FileTitle.Visibility = ShowFileName ? Visibility.Visible : Visibility.Collapsed;
        var scroll = IsFullView ? VisualTreeSearch.FindDescendant<ScrollViewer>(Lines) : null;
        var offset = scroll?.VerticalOffset ?? 0;
        Lines.ItemsSource = document.Lines;
        if (scroll is not null) DispatcherQueue.TryEnqueue(() => { Lines.UpdateLayout(); scroll.ChangeView(null, offset, null, true); });
        Notice.Text = document.Notice;
        Notice.Visibility = document.Notice.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ResizeViewport();
    }
    private void ResizeViewport() => Lines.MaxHeight = IsFullView ? Math.Max(80, ActualHeight - (Notice.Visibility == Visibility.Visible ? 60 : 2)) : 360;
    private async void OnExpand(object sender, RoutedEventArgs args)
    {
        if (dialogOpen) return;
        dialogOpen = true;
        try
        {
            var content = new Models.Conversations.FileDiffContent(FileTitle.Text, Patch ?? "", "Captured edits · unchanged sections may be omitted", "Before", "After");
            await new Views.FileDiffDialog(content) { XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception) { Notice.Text = "Couldn't open the diff. Close any other dialog and try again."; Notice.Visibility = Visibility.Visible; }
        finally { dialogOpen = false; }
    }
    private void OnCopy(object sender, RoutedEventArgs args)
    {
        try
        {
            var package = new DataPackage(); package.SetText(Patch ?? ""); Clipboard.SetContent(package);
            if (sender is CopyFeedbackButton button) button.ShowCopied();
        }
        catch (Exception) { Notice.Text = "Couldn't copy the diff. Try again."; Notice.Visibility = Visibility.Visible; }
    }
}
