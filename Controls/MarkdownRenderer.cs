using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace PiAgentGui.Controls;

/// <summary>Renders CommonMark into native controls; HTML is text and images are not fetched.</summary>
internal static class MarkdownRenderer
{
    internal static bool UpdateBlock(FrameworkElement element, Markdig.Syntax.Block block)
    {
        if (element is CodeBlockView codeView && block is CodeBlock code)
        {
            var label = code is FencedCodeBlock fenced
                ? string.Join(" ", new[] { fenced.Info, fenced.Arguments }.Where(value => !string.IsNullOrWhiteSpace(value))) : "Code";
            codeView.Update(label, code.Lines.ToString());
            return true;
        }
        if (element is RichTextBlock text && block is ParagraphBlock paragraph)
        {
            var target = (Paragraph)text.Blocks[0];
            target.Inlines.Clear();
            if (paragraph.Inline is not null) AddInlines(target.Inlines, paragraph.Inline);
            return true;
        }
        return false;
    }
    internal static StackPanel Render(ContainerBlock document)
    {
        var panel = new StackPanel { Spacing = 9 };
        foreach (var block in document) panel.Children.Add(RenderBlock(block));
        return panel;
    }

    internal static FrameworkElement RenderBlock(Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case FencedCodeBlock code:
                var label = string.Join(" ", new[] { code.Info, code.Arguments }.Where(value => !string.IsNullOrWhiteSpace(value)));
                return new CodeBlockView(label, code.Lines.ToString());
            case CodeBlock code:
                return new CodeBlockView("Code", code.Lines.ToString());
            case HeadingBlock heading:
                var title = Text(heading.Inline);
                title.FontSize = heading.Level switch { 1 => 22, 2 => 19.5, 3 => 16.5, _ => 15 };
                title.FontWeight = FontWeights.SemiBold;
                title.Margin = new Thickness(0, 8, 0, 0);
                return title;
            case ParagraphBlock paragraph:
                return Text(paragraph.Inline);
            case QuoteBlock quote:
                return new Border { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 2, 0, 2),
                    BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"], Child = Render(quote) };
            case ListBlock list:
                var items = new StackPanel { Spacing = 5 };
                var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                foreach (var child in list)
                {
                    var row = new Grid { ColumnSpacing = 8 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.Children.Add(new TextBlock { Text = list.IsOrdered ? $"{number++}." : "•", FontSize = 13, MinWidth = 18 });
                    var body = Render((ContainerBlock)child);
                    Grid.SetColumn(body, 1);
                    row.Children.Add(body);
                    items.Children.Add(row);
                }
                return items;
            case ThematicBreakBlock:
                return new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6), Background = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"] };
            case LeafBlock leaf:
                return new TextBlock { Text = leaf.Lines.ToString(), FontSize = 13, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            case ContainerBlock container:
                return Render(container);
            default:
                return new TextBlock();
        }
    }

    private static RichTextBlock Text(ContainerInline? source)
    {
        var text = new RichTextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        var paragraph = new Paragraph();
        if (source is not null) AddInlines(paragraph.Inlines, source);
        text.Blocks.Add(paragraph);
        return text;
    }

    private static void AddInlines(InlineCollection target, ContainerInline source)
    {
        foreach (var inline in source)
        {
            switch (inline)
            {
                case LiteralInline literal: target.Add(new Run { Text = literal.Content.ToString() }); break;
                case CodeInline code:
                    target.Add(new Run { Text = code.Content, FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.SemiBold }); break;
                case LineBreakInline line:
                    if (line.IsHard) target.Add(new LineBreak()); else target.Add(new Run { Text = " " });
                    break;
                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                    else span.FontStyle = FontStyle.Italic;
                    AddInlines(span.Inlines, emphasis);
                    target.Add(span);
                    break;
                case LinkInline link:
                    if (!link.IsImage && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
                    {
                        var hyperlink = new Hyperlink { NavigateUri = uri };
                        AddInlines(hyperlink.Inlines, link);
                        target.Add(hyperlink);
                    }
                    else AddInlines(target, link);
                    break;
                case AutolinkInline auto:
                    if (Uri.TryCreate(auto.IsEmail ? "mailto:" + auto.Url : auto.Url, UriKind.Absolute, out var address) && address.Scheme is "http" or "https" or "mailto")
                    {
                        var hyperlink = new Hyperlink { NavigateUri = address };
                        hyperlink.Inlines.Add(new Run { Text = auto.Url });
                        target.Add(hyperlink);
                    }
                    else target.Add(new Run { Text = auto.Url });
                    break;
                case HtmlInline html: target.Add(new Run { Text = html.Tag }); break;
                case ContainerInline container: AddInlines(target, container); break;
            }
        }
    }
}
