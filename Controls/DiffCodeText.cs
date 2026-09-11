using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using PiAgentGui.Models.Conversations;
using Windows.UI.ViewManagement;

namespace PiAgentGui.Controls;

/// <summary>Renders precomputed syntax spans without running a tokenizer on the UI thread.</summary>
public sealed class DiffCodeText : UserControl
{
    private readonly TextBlock text = new() { FontFamily = new FontFamily("Consolas"), FontSize = 11, TextWrapping = TextWrapping.NoWrap, IsTextSelectionEnabled = true };
    public static readonly DependencyProperty LineProperty = DependencyProperty.Register(nameof(Line), typeof(DiffLine), typeof(DiffCodeText),
        new PropertyMetadata(null, (sender, _) => ((DiffCodeText)sender).Render()));
    public DiffLine? Line { get => (DiffLine?)GetValue(LineProperty); set => SetValue(LineProperty, value); }
    public DiffCodeText()
    {
        Content = text;
        Loaded += (_, _) => Render(); ActualThemeChanged += (_, _) => Render();
    }
    private void Render()
    {
        var line = Line;
        text.Text = line?.Text ?? "";
        if (line?.SyntaxTokens is not { Count: > 0 and <= 500 } tokens || new AccessibilitySettings().HighContrast) return;
        if (string.Concat(tokens.Select(token => token.Text)) != line.Text) return;
        text.Inlines.Clear();
        var brushes = new Dictionary<string, Brush?>();
        foreach (var token in tokens)
        {
            if (!brushes.TryGetValue(token.Kind, out var brush)) brushes[token.Kind] = brush = CodeSyntaxColors.Get(token.Kind, ActualTheme == ElementTheme.Dark);
            var run = new Run { Text = token.Text };
            if (brush is not null) run.Foreground = brush;
            text.Inlines.Add(run);
        }
    }
}
