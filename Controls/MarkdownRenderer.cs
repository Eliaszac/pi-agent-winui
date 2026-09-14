using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using PiAgentGui.Services.Files;
using PiAgentGui.Utilities;

namespace PiAgentGui.Controls;

/// <summary>Renders CommonMark into native controls; HTML is text and images are not fetched.</summary>
internal static class MarkdownRenderer
{
    internal static bool UpdateBlock(FrameworkElement element, Markdig.Syntax.Block block)
    {
        if (element is CodeBlockView codeView && block is CodeBlock code)
        {
            // Completed blocks bind actions to an immutable code snapshot; replace that binding if code changes.
            if (codeView.HasSnippet) return false;
            var label = code is FencedCodeBlock fenced
                ? string.Join(" ", new[] { fenced.Info, fenced.Arguments }.Where(value => !string.IsNullOrWhiteSpace(value))) : "Code";
            codeView.Update(label, code.Lines.ToString());
            return true;
        }
        if (element is RichTextBlock text && block is ParagraphBlock paragraph)
        {
            var target = (Paragraph)text.Blocks[0];
            target.Inlines.Clear();
            if (paragraph.Inline is not null) AddInlines(target.Inlines, paragraph.Inline, text);
            return true;
        }
        return false;
    }
    internal static StackPanel Render(ContainerBlock document, Func<string, string, string, ViewModels.Conversations.SnippetViewModel>? snippets = null, bool prose = true, WorkspaceFileLinks? files = null)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var block in document) panel.Children.Add(RenderBlock(block, snippets, prose, files));
        return panel;
    }

    internal static FrameworkElement RenderBlock(Markdig.Syntax.Block block, Func<string, string, string, ViewModels.Conversations.SnippetViewModel>? snippets = null, bool prose = true, WorkspaceFileLinks? files = null)
    {
        switch (block)
        {
            case Table table:
                return new MarkdownTableView(table, files);
            case FencedCodeBlock code:
                var label = string.Join(" ", new[] { code.Info, code.Arguments }.Where(value => !string.IsNullOrWhiteSpace(value)));
                var fencedView = new CodeBlockView(label, code.Lines.ToString());
                if (snippets is not null) fencedView.AttachSnippet(snippets(code.Span.Start.ToString(), label, code.Lines.ToString()));
                return fencedView;
            case CodeBlock code:
                var codeView = new CodeBlockView("Code", code.Lines.ToString());
                if (snippets is not null) codeView.AttachSnippet(snippets(code.Span.Start.ToString(), "text", code.Lines.ToString()));
                return codeView;
            case HeadingBlock heading:
                var title = Text(heading.Inline, prose, files);
                title.FontSize = (heading.Level switch { 1 => 28, 2 => 23, 3 => 19, _ => 16 }) * ReadingPreferences.Scale;
                title.LineHeight = title.FontSize * 1.35;
                title.FontWeight = prose ? ReadingPreferences.EmphasisWeight : FontWeights.SemiBold;
                title.Margin = new Thickness(0, prose ? 6 : 12, 0, 0);
                return title;
            case ParagraphBlock paragraph:
                return Text(paragraph.Inline, prose, files);
            case QuoteBlock quote:
                return new Border { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 2, 0, 2),
                    BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"], Child = Render(quote, snippets, prose, files) };
            case ListBlock list:
                var depth = 0;
                for (var ancestor = list.Parent; ancestor is not null; ancestor = ancestor.Parent)
                    if (ancestor is ListBlock) depth++;
                var items = new StackPanel { Spacing = list.IsLoose ? (prose ? 14 : 12) : (prose ? 8 : 6),
                    Margin = new Thickness(depth > 0 ? 16 : 0, 0, 0, 0) };
                var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                var separator = prose ? "\u2002" : " ";
                var markers = Enumerable.Range(0, list.Count).Select(index => list.IsOrdered ? $"{number + index}.{separator}" : "–" + separator).ToArray();
                var widths = markers.Select(marker =>
                {
                    var measure = new TextBlock { Text = marker, FontSize = ReadingPreferences.Body };
                    measure.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                    return measure.DesiredSize.Width;
                }).ToArray();
                var gutter = widths.Length > 0 ? widths.Max() : 0;
                for (var index = 0; index < list.Count; index++)
                {
                    var body = Render((ContainerBlock)list[index], snippets, prose, files);
                    if (body.Children.FirstOrDefault() is not RichTextBlock)
                        body.Children.Insert(0, Text(null, prose, files));
                    var first = (RichTextBlock)body.Children[0];
                    first.Padding = new Thickness(gutter, 0, 0, 0);
                    var paragraph = (Paragraph)first.Blocks[0];
                    paragraph.TextIndent = -widths[index];
                    paragraph.Inlines.Insert(0, new Run { Text = markers[index] });
                    foreach (var element in body.Children.Skip(1).OfType<FrameworkElement>())
                        element.Margin = new Thickness(element.Margin.Left + gutter, element.Margin.Top, element.Margin.Right, element.Margin.Bottom);
                    items.Children.Add(body);
                }
                return items;
            case ThematicBreakBlock:
                return new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6), Background = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"] };
            case LeafBlock leaf:
                return new TextBlock { Text = leaf.Lines.ToString(), FontSize = ReadingPreferences.Body, LineHeight = 24 * ReadingPreferences.Scale, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            case ContainerBlock container:
                return Render(container, snippets, prose, files);
            default:
                return new TextBlock();
        }
    }

    internal static RichTextBlock Text(ContainerInline? source, bool prose = false, WorkspaceFileLinks? files = null)
    {
        var text = new RichTextBlock { Tag = files, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, FontSize = ReadingPreferences.Body,
            LineHeight = 24 * ReadingPreferences.Scale, LineStackingStrategy = LineStackingStrategy.MaxHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (prose)
        {
            text.FontWeight = ReadingPreferences.BodyWeight;
            text.MaxWidth = 720 * ReadingPreferences.Scale;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            text.LineHeight = 27 * ReadingPreferences.Scale;
        }
        var paragraph = new Paragraph();
        if (source is not null) AddInlines(paragraph.Inlines, source, text);
        text.Blocks.Add(paragraph);
        return text;
    }

    private static void AddText(InlineCollection target, string value, RichTextBlock owner, bool recognizeFiles, bool code)
    {
        void Plain(string text, bool monospace)
        {
            var run = new Run { Text = text };
            if (monospace) { run.FontFamily = new FontFamily("Consolas"); run.FontSize = 14 * ReadingPreferences.Scale; }
            target.Add(run);
        }
        if (!recognizeFiles || owner.Tag is not WorkspaceFileLinks files) { Plain(value, code); return; }
        var offset = 0;
        foreach (var mention in FileMentionParser.Find(value))
        {
            if (mention.Start > offset) Plain(value[offset..mention.Start], code);
            AddFileMention(target, files, mention, owner);
            offset = mention.Start + mention.Length;
        }
        if (offset < value.Length) Plain(value[offset..], code);
    }

    private static void AddInlines(InlineCollection target, ContainerInline source, RichTextBlock owner, bool recognizeFiles = true)
    {
        foreach (var inline in source)
        {
            switch (inline)
            {
                case LiteralInline literal: AddText(target, literal.Content.ToString(), owner, recognizeFiles, false); break;
                case CodeInline code:
                    AddText(target, code.Content, owner, recognizeFiles, true); break;
                case TaskList task:
                    target.Add(new Run { Text = task.Checked ? "☑ " : "☐ " });
                    break;
                case LineBreakInline line:
                    if (line.IsHard) target.Add(new LineBreak()); else target.Add(new Run { Text = " " });
                    break;
                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                    else if (emphasis.DelimiterCount >= 2) span.FontWeight = owner.FontWeight.Weight >= 600 ? FontWeights.Bold : FontWeights.SemiBold;
                    else span.FontStyle = FontStyle.Italic;
                    AddInlines(span.Inlines, emphasis, owner, recognizeFiles);
                    target.Add(span);
                    break;
                case LinkInline link:
                    if (!link.IsImage && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
                    {
                        var label = new TextBlock();
                        AddInlines(label.Inlines, link, owner, recognizeFiles: false);
                        target.Add(new InlineUIContainer { Child = MarkdownLinkActions.Create(uri, label, owner) });
                    }
                    else if (!link.IsImage && owner.Tag is WorkspaceFileLinks fileLinks && link.Url is { } destination
                        && FileMentionParser.Find(destination) is { Count: 1 } mentions && mentions[0].Length == destination.Length)
                    {
                        var mention = mentions[0] with { Text = MarkdownCellText.ReadInline(link) };
                        AddFileMention(target, fileLinks, mention, owner);
                    }
                    else AddInlines(target, link, owner, recognizeFiles: false);
                    break;
                case AutolinkInline auto:
                    if (Uri.TryCreate(auto.IsEmail ? "mailto:" + auto.Url : auto.Url, UriKind.Absolute, out var address) && address.Scheme is "http" or "https" or "mailto")
                    {
                        target.Add(new InlineUIContainer { Child = MarkdownLinkActions.Create(address, new TextBlock { Text = auto.Url }, owner) });
                    }
                    else target.Add(new Run { Text = auto.Url });
                    break;
                case HtmlInline html: target.Add(new Run { Text = html.Tag }); break;
                case ContainerInline container: AddInlines(target, container, owner, recognizeFiles); break;
            }
        }
    }

    private static void AddFileMention(InlineCollection target, WorkspaceFileLinks files, FileMention mention, RichTextBlock owner)
    {
        var link = new MarkdownFileLink(files, mention, owner);
        var container = new InlineUIContainer { Child = link };
        link.MissingFileResolved += foreground =>
        {
            var index = target.IndexOf(container);
            if (index < 0) return;
            // A non-interactive filename should use the paragraph's native text baseline.
            var text = new Run { Text = mention.Text, FontFamily = new FontFamily("Consolas"),
                FontSize = 14 * ReadingPreferences.Scale, Foreground = foreground };
            target.RemoveAt(index);
            target.Insert(index, text);
        };
        target.Add(container);
    }
}
