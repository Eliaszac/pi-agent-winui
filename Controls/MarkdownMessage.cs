using Markdig;
using Microsoft.UI.Dispatching;
using PiAgentGui.Utilities;

namespace PiAgentGui.Controls;

/// <summary>Coalesces streamed Markdown updates into native text controls.</summary>
public sealed class MarkdownMessage : UserControl
{
    public static readonly DependencyProperty FileLinksProperty = DependencyProperty.Register(nameof(FileLinks), typeof(Services.Files.WorkspaceFileLinks),
        typeof(MarkdownMessage), new PropertyMetadata(null, (sender, _) => ((MarkdownMessage)sender).ResetActions()));
    public Services.Files.WorkspaceFileLinks? FileLinks
    { get => (Services.Files.WorkspaceFileLinks?)GetValue(FileLinksProperty); set => SetValue(FileLinksProperty, value); }

    public static readonly DependencyProperty SnippetFactoryProperty = DependencyProperty.Register(nameof(SnippetFactory), typeof(Func<string, string, string, ViewModels.Conversations.SnippetViewModel>),
        typeof(MarkdownMessage), new PropertyMetadata(null, (sender, _) => ((MarkdownMessage)sender).ResetActions()));
    public Func<string, string, string, ViewModels.Conversations.SnippetViewModel>? SnippetFactory
    { get => (Func<string, string, string, ViewModels.Conversations.SnippetViewModel>?)GetValue(SnippetFactoryProperty); set => SetValue(SnippetFactoryProperty, value); }
    public static readonly DependencyProperty SnippetsEnabledProperty = DependencyProperty.Register(nameof(SnippetsEnabled), typeof(bool),
        typeof(MarkdownMessage), new PropertyMetadata(false, (sender, _) => ((MarkdownMessage)sender).ResetActions()));
    public bool SnippetsEnabled { get => (bool)GetValue(SnippetsEnabledProperty); set => SetValue(SnippetsEnabledProperty, value); }
    private void ResetActions() { rendered = null; blockSources.Clear(); panel.Children.Clear(); Schedule(); }
    public event EventHandler? ContentRendered;
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(MarkdownMessage), new PropertyMetadata("", (sender, _) => ((MarkdownMessage)sender).Schedule()));
    public static readonly DependencyProperty ResponsiveWidthProperty = DependencyProperty.Register(nameof(ResponsiveWidth), typeof(bool),
        typeof(MarkdownMessage), new PropertyMetadata(false, (sender, _) => ((MarkdownMessage)sender).InvalidateMeasure()));
    public bool ResponsiveWidth { get => (bool)GetValue(ResponsiveWidthProperty); set => SetValue(ResponsiveWidthProperty, value); }
    private readonly DispatcherQueueTimer timer;
    private string? rendered;
    private bool renderQueued;
    private double renderedBodySize;
    private double renderedCodeSize;
    private readonly StackPanel panel = new() { Spacing = 16, MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly List<string> blockSources = [];
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public MarkdownMessage()
    {
        Content = panel;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => QueueRender();
        Loaded += (_, _) =>
        {
            ReadingPreferences.TypographyChanged += OnReadingChanged;
            if (renderedBodySize != ReadingPreferences.Body || renderedCodeSize != ReadingPreferences.Code)
                OnReadingChanged(null, EventArgs.Empty);
            QueueRender();
        };
        Unloaded += (_, _) => { timer.Stop(); ReadingPreferences.TypographyChanged -= OnReadingChanged; };
        ActualThemeChanged += (_, _) => { rendered = null; blockSources.Clear(); panel.Children.Clear(); Render(); };
    }

    private void OnReadingChanged(object? sender, EventArgs args) => ResetActions();

    private void Schedule() { if (IsLoaded && !timer.IsRunning) timer.Start(); }

    private void QueueRender()
    {
        if (renderQueued || !IsLoaded) return;
        renderQueued = true;
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            renderQueued = false;
            Render();
        })) renderQueued = false;
    }

    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        panel.MaxWidth = ResponsiveWidth && double.IsFinite(availableSize.Width)
            ? Math.Min(availableSize.Width, Math.Max(680, availableSize.Width * 0.7))
            : 960;
        return base.MeasureOverride(availableSize);
    }

    private void Render()
    {
        if (!IsLoaded || rendered == Text) return;
        var source = Text ?? "";
        var document = ResponseMarkdown.Parse(source);
        for (var index = 0; index < document.Count; index++)
        {
            var block = document[index];
            var signature = MarkdownBlockSignature.Create(block.GetType().Name, source, block.Span.Start, block.Span.End);
            if (index < blockSources.Count && blockSources[index] == signature) continue;
            if (index < panel.Children.Count && blockSources[index].StartsWith(block.GetType().Name + ":", StringComparison.Ordinal)
                && MarkdownRenderer.UpdateBlock((FrameworkElement)panel.Children[index], block))
            {
                blockSources[index] = signature;
                continue;
            }
            var element = MarkdownRenderer.RenderBlock(block, SnippetsEnabled ? SnippetFactory : null, files: FileLinks);
            if (index < panel.Children.Count) { panel.Children[index] = element; blockSources[index] = signature; }
            else { panel.Children.Add(element); blockSources.Add(signature); }
        }
        while (panel.Children.Count > document.Count)
        {
            panel.Children.RemoveAt(panel.Children.Count - 1);
            blockSources.RemoveAt(blockSources.Count - 1);
        }
        rendered = source;
        renderedBodySize = ReadingPreferences.Body;
        renderedCodeSize = ReadingPreferences.Code;
        ContentRendered?.Invoke(this, EventArgs.Empty);
    }
}
