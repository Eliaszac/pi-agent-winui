using System.Text;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.TaskLists;

namespace PiAgentGui.Utilities;

/// <summary>Extracts the visible text of Markdown cells without link destinations or emphasis syntax.</summary>
public static class MarkdownCellText
{
    public static string Read(ContainerBlock cell)
    {
        var parts = new List<string>();
        foreach (var block in cell)
        {
            if (block is LeafBlock leaf)
            {
                var text = new StringBuilder();
                if (leaf.Inline is not null) Append(text, leaf.Inline);
                else text.Append(leaf.Lines.ToString());
                parts.Add(text.ToString());
            }
            else if (block is ContainerBlock container) parts.Add(Read(container));
        }
        return string.Join("\n", parts).Trim();
    }

    private static void Append(StringBuilder text, ContainerInline source)
    {
        foreach (var inline in source)
        {
            switch (inline)
            {
                case LiteralInline literal: text.Append(literal.Content); break;
                case CodeInline code: text.Append(code.Content); break;
                case LineBreakInline line: text.Append(line.IsHard ? "\n" : " "); break;
                case AutolinkInline link: text.Append(link.Url); break;
                case HtmlInline html: text.Append(html.Tag); break;
                case TaskList task: text.Append(task.Checked ? "☑ " : "☐ "); break;
                case ContainerInline container: Append(text, container); break;
            }
        }
    }
}
