using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Controls;

/// <summary>A selectable, horizontally scrollable code block with an exact-text copy action.</summary>
public sealed class CodeBlockView : UserControl
{
    private readonly TextBlock codeText;
    private readonly TextBlock labelText;
    private string currentCode;
    internal void Update(string label, string code)
    {
        currentCode = code;
        codeText.Text = code;
        labelText.Text = string.IsNullOrWhiteSpace(label) ? "Code" : label;
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
        Loaded += (_, _) => ApplyTheme();
        ActualThemeChanged += (_, _) => ApplyTheme();
    }
}
