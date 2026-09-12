using Markdig;
using Markdig.Syntax;

namespace PiAgentGui.Utilities;

/// <summary>Shared parsing contract for response Markdown, including GitHub-style tables.</summary>
public static class ResponseMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseTaskLists().UseEmphasisExtras().Build();

    public static MarkdownDocument Parse(string source) => Markdown.Parse(source, Pipeline);
}
