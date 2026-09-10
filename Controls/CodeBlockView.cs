using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml.Documents;
using PiAgentGui.Utilities;
using Windows.UI.ViewManagement;

namespace PiAgentGui.Controls;

/// <summary>A selectable, horizontally scrollable code block with an exact-text copy action.</summary>
public sealed class CodeBlockView : UserControl
{
    private readonly TextBlock codeText;
    private readonly TextBlock labelText;
    private string currentCode;
    private readonly CodeSyntaxHighlighter highlighter = new();
    private CancellationTokenSource? highlighting;
    private readonly AccessibilitySettings accessibility = new();
    internal void Update(string label, string code)
    {
        var changed = currentCode != code || labelText.Text != label;
        var previousCode = currentCode;
        currentCode = code;
        labelText.Text = string.IsNullOrWhiteSpace(label) ? "Code" : label;
        if (changed)
        {
            if (code.StartsWith(previousCode, StringComparison.Ordinal) && codeText.Inlines.Count > 0)
                codeText.Inlines.Add(new Run { Text = code[previousCode.Length..] });
            else codeText.Text = code;
            RefreshHighlighting();
        }
    }

    private async void RefreshHighlighting()
    {
        highlighting?.Cancel();
        if (!IsLoaded) return;
        var request = new CancellationTokenSource();
        highlighting = request;
        var code = currentCode;
        var label = labelText.Text;
        try
        {
            if (accessibility.HighContrast) { codeText.Text = code; return; }
            await Task.Delay(180, request.Token);
            var tokens = await Task.Run(() => highlighter.Highlight(label, code, request.Token), request.Token);
            if (request.IsCancellationRequested || !IsLoaded) return;
            codeText.Inlines.Clear();
            var brushes = new Dictionary<string, Brush?>();
            foreach (var token in tokens)
            {
                if (!brushes.TryGetValue(token.Kind, out var brush))
                    brushes[token.Kind] = brush = CodeSyntaxColors.Get(token.Kind, ActualTheme == ElementTheme.Dark);
                var run = new Run { Text = token.Text };
                if (brush is not null) run.Foreground = brush;
                codeText.Inlines.Add(run);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Formatting must never interrupt a streamed response or change its contents.
            if (!request.IsCancellationRequested && IsLoaded) codeText.Text = currentCode;
        }
        finally { if (ReferenceEquals(highlighting, request)) highlighting = null; request.Dispose(); }
    }

    public CodeBlockView(string label, string code)
    {
        currentCode = code;
        var panel = new StackPanel();
        var header = new Grid { Padding = new Thickness(12, 4, 6, 4), ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        labelText = new TextBlock { Text = string.IsNullOrWhiteSpace(label) ? "Code" : label,
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        header.Children.Add(labelText);
        var copy = new CopyFeedbackButton { Style = (Style)Application.Current.Resources["ShellIconButtonStyle"] };
        AutomationProperties.SetName(copy, "Copy code");
        ToolTipService.SetToolTip(copy, "Copy code");
        copy.Click += (_, _) =>
        {
            try
            {
                var data = new DataPackage();
                data.SetText(currentCode);
                Clipboard.SetContent(data);
                copy.ShowCopied();
            }
            catch (System.Runtime.InteropServices.COMException) { ToolTipService.SetToolTip(copy, "Couldn't copy. Try again."); }
        };
        Grid.SetColumn(copy, 1);
        header.Children.Add(copy);
        panel.Children.Add(header);
        var text = new TextBlock { Text = code, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            IsTextSelectionEnabled = true, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(11, 7, 11, 11) };
        codeText = text;
        panel.Children.Add(new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled });
        var border = new Border { Child = panel, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        Content = border;
        void ApplyTheme()
        {
            border.Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];
            border.BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
            header.Background = (Brush)Application.Current.Resources["ControlFillColorTertiaryBrush"];
        }
        Loaded += (_, _) => { ApplyTheme(); RefreshHighlighting(); };
        Unloaded += (_, _) => highlighting?.Cancel();
        ActualThemeChanged += (_, _) => { ApplyTheme(); RefreshHighlighting(); };
    }
}
