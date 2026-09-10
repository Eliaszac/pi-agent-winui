using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Documents;

namespace PiAgentGui.Controls;

/// <summary>Displays a selectable unified patch with themed additions and removals.</summary>
public sealed class FileDiffView : UserControl
{
    private string? renderedPatch;
    public static readonly DependencyProperty PatchProperty = DependencyProperty.Register(nameof(Patch), typeof(string),
        typeof(FileDiffView), new PropertyMetadata(null, (sender, _) => ((FileDiffView)sender).Render()));
    public string? Patch { get => (string?)GetValue(PatchProperty); set => SetValue(PatchProperty, value); }

    public FileDiffView()
    {
        Loaded += (_, _) => Render();
        ActualThemeChanged += (_, _) => { renderedPatch = null; Render(); };
    }

    private void Render()
    {
        if (!IsLoaded || (Content is not null && renderedPatch == Patch)) return;
        renderedPatch = Patch;
        var text = new RichTextBlock { IsTextSelectionEnabled = true, FontFamily = new FontFamily("Consolas"), FontSize = 11,
            TextWrapping = TextWrapping.NoWrap, Padding = new Thickness(12) };
        var paragraph = new Paragraph();
        foreach (var line in (Patch ?? "").Split('\n'))
        {
            var run = new Run { Text = line.TrimEnd('\r') };
            if (line.StartsWith('+')) run.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            else if (line.StartsWith('-')) run.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            else if (line.StartsWith("@@", StringComparison.Ordinal)) run.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            paragraph.Inlines.Add(run);
            paragraph.Inlines.Add(new LineBreak());
        }
        text.Blocks.Add(paragraph);
        Content = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            Child = new ScrollViewer { Content = text, MaxHeight = 360, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
}
