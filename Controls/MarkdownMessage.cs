using Markdig;
using Microsoft.UI.Dispatching;

namespace PiAgentGui.Controls;

/// <summary>Coalesces streamed Markdown updates into native text controls.</summary>
public sealed class MarkdownMessage : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(MarkdownMessage), new PropertyMetadata("", (sender, _) => ((MarkdownMessage)sender).Schedule()));
    private readonly DispatcherQueueTimer timer;
    private string? rendered;
    private readonly StackPanel panel = new() { Spacing = 9 };
    private readonly List<string> blockSources = [];
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public MarkdownMessage()
    {
        Content = panel;
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => Render();
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => timer.Stop();
        ActualThemeChanged += (_, _) => { rendered = null; blockSources.Clear(); panel.Children.Clear(); Render(); };
    }

    private void Schedule() { if (IsLoaded && !timer.IsRunning) timer.Start(); }

    private void Render()
    {
        if (!IsLoaded || rendered == Text) return;
        var source = Text ?? "";
        var document = Markdown.Parse(source);
        for (var index = 0; index < document.Count; index++)
        {
            var block = document[index];
            var signature = block.GetType().Name + ":" + source.Substring(block.Span.Start, block.Span.Length);
            if (index < blockSources.Count && blockSources[index] == signature) continue;
            if (index < panel.Children.Count && blockSources[index].StartsWith(block.GetType().Name + ":", StringComparison.Ordinal)
                && MarkdownRenderer.UpdateBlock((FrameworkElement)panel.Children[index], block))
            {
                blockSources[index] = signature;
                continue;
            }
            var element = MarkdownRenderer.RenderBlock(block);
            if (index < panel.Children.Count) { panel.Children[index] = element; blockSources[index] = signature; }
            else { panel.Children.Add(element); blockSources.Add(signature); }
        }
        while (panel.Children.Count > document.Count)
        {
            panel.Children.RemoveAt(panel.Children.Count - 1);
            blockSources.RemoveAt(blockSources.Count - 1);
        }
        rendered = source;
    }
}
