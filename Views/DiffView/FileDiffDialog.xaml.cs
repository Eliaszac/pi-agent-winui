using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Views;

public sealed partial class FileDiffDialog : Controls.ActionContentDialog
{
    private readonly FileDiffContent content;
    private ScrollViewer? leftScroll;
    private ScrollViewer? rightScroll;
    private bool changingLayout;
    private bool preferSplit = true;
    private bool closed;
    private readonly CancellationTokenSource lifetime = new();
    private UnifiedDiffDocument? document;
    private readonly HashSet<int> revealed = [];
    private double? expectedLeft;
    private double? expectedRight;

    public FileDiffDialog(FileDiffContent content)
    {
        this.content = content; InitializeComponent();
        Title = content.FileName; Description.Text = content.Description;
        LeftLabel.Text = content.LeftLabel; RightLabel.Text = content.RightLabel;
        Opened += OnOpened;
        Closed += (_, _) =>
        {
            closed = true;
            lifetime.Cancel();
            if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged;
            if (leftScroll is not null) leftScroll.ViewChanged -= OnLeftScrolled;
            if (rightScroll is not null) rightScroll.ViewChanged -= OnRightScrolled;
        };
    }
    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        XamlRoot.Changed += OnRootChanged; ResizeDialog();
        try
        {
            document = await Task.Run(() => DiffSyntaxHighlighter.Highlight(UnifiedDiffParser.Parse(content.Patch, 20000), content.FileName, lifetime.Token));
            if (closed) return;
            ApplyRows();
            Notice.Text = content.Notice.Length > 0 ? content.Notice : document.Notice;
            Notice.Visibility = Notice.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception) { if (!closed) { Notice.Text = "Couldn't display this diff."; Notice.Visibility = Visibility.Visible; } }
    }
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ResizeDialog();
    private void ResizeDialog()
    {
        Body.Width = Math.Max(160, Math.Min(1440, XamlRoot.Size.Width - 120));
        Body.Height = Math.Max(120, XamlRoot.Size.Height - 150);
        ApplyLayout();
    }
    private void OnLayoutChanged(object sender, SelectionChangedEventArgs args)
    {
        if (changingLayout || InlineView is null) return;
        preferSplit = LayoutMode.SelectedIndex == 0;
        ApplyLayout();
    }
    private void OnChangesOnlyChanged(object sender, RoutedEventArgs args) => ApplyRows();
    private void OnRevealContext(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: DiffLine gap }) RevealContext(gap);
    }
    private void RevealContext(DiffLine gap)
    {
        DiffContextFilter.RevealNext(gap, revealed);
        ApplyRows(false);
    }
    private void OnInlineReveal(object? sender, DiffLine gap) => RevealContext(gap);
    private void ApplyRows(bool resetScroll = true)
    {
        if (document is null) return;
        var leftOffset = resetScroll ? 0 : leftScroll?.VerticalOffset ?? 0;
        var rightOffset = resetScroll ? 0 : rightScroll?.VerticalOffset ?? 0;
        var rows = ChangesOnly.IsChecked == true ? DiffContextFilter.ChangesOnly(document.Lines, revealed: revealed) : document.Lines;
        var split = SplitDiffBuilder.Build(rows);
        LeftLines.ItemsSource = split.Left; RightLines.ItemsSource = split.Right;
        InlineView.Document = document with { Lines = rows };
        expectedLeft = expectedRight = null;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (closed) return;
            LeftLines.UpdateLayout(); RightLines.UpdateLayout();
            leftScroll?.ChangeView(null, leftOffset, null, true); rightScroll?.ChangeView(null, rightOffset, null, true);
        });
    }
    private void ApplyLayout()
    {
        var narrow = Body.Width < 760;
        var split = preferSplit && !narrow;
        changingLayout = true;
        LayoutMode.SelectedIndex = split ? 0 : 1;
        ((ComboBoxItem)LayoutMode.Items[0]).IsEnabled = !narrow;
        changingLayout = false;
        SplitView.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        InlineView.Visibility = split ? Visibility.Collapsed : Visibility.Visible;
        if (!split) { InlineView.FileName = content.FileName; InlineView.Patch = content.Patch; }
    }
    private void OnListsLoaded(object sender, RoutedEventArgs args)
    {
        if (leftScroll is null && Controls.VisualTreeSearch.FindDescendant<ScrollViewer>(LeftLines) is { } left) { leftScroll = left; left.ViewChanged += OnLeftScrolled; }
        if (rightScroll is null && Controls.VisualTreeSearch.FindDescendant<ScrollViewer>(RightLines) is { } right) { rightScroll = right; right.ViewChanged += OnRightScrolled; }
    }
    private void OnLeftScrolled(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        if (leftScroll is null || rightScroll is null) return;
        if (expectedLeft is { } value && Math.Abs(leftScroll.VerticalOffset - value) < 0.5) { expectedLeft = null; return; }
        expectedLeft = null;
        if (Math.Abs(rightScroll.VerticalOffset - leftScroll.VerticalOffset) < 0.5) return;
        expectedRight = Math.Min(leftScroll.VerticalOffset, rightScroll.ScrollableHeight);
        rightScroll.ChangeView(null, expectedRight, null, true);
    }
    private void OnRightScrolled(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        if (leftScroll is null || rightScroll is null) return;
        if (expectedRight is { } value && Math.Abs(rightScroll.VerticalOffset - value) < 0.5) { expectedRight = null; return; }
        expectedRight = null;
        if (Math.Abs(leftScroll.VerticalOffset - rightScroll.VerticalOffset) < 0.5) return;
        expectedLeft = Math.Min(rightScroll.VerticalOffset, leftScroll.ScrollableHeight);
        leftScroll.ChangeView(null, expectedLeft, null, true);
    }
    private void OnCopy(object sender, RoutedEventArgs args)
    {
        try { var data = new DataPackage(); data.SetText(content.Patch); Clipboard.SetContent(data); ((Controls.CopyFeedbackButton)sender).ShowCopied(); }
        catch (Exception) { Notice.Text = "Couldn't copy the diff. Try again."; Notice.Visibility = Visibility.Visible; }
    }
}
