using Microsoft.VisualStudio.TestTools.UnitTesting;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ResponseMarkdownTests
{
    [TestMethod]
    public void AgentComparisonRendersAsTableWithInlineFormattingAndAlignment()
    {
        var document = ResponseMarkdown.Parse("""
            ## Features

            | Area | Missing feature | Evidence |
            | :--- | :---: | ---: |
            | Remote | **File editing** | `EXECUTION-TARGETS.md` |
            | Git | A literal \| pipe | [Docs](https://example.com) |
            """);
        Assert.IsInstanceOfType<HeadingBlock>(document[0]);
        var table = (Table)document[1];
        Assert.AreEqual(3, table.Count);
        Assert.AreEqual(3, table.ColumnDefinitions.Count);
        Assert.AreEqual(TableColumnAlign.Right, table.ColumnDefinitions[2].Alignment);
        Assert.AreEqual(3, ((TableRow)table[2]).Count);
    }

    [TestMethod]
    public void StreamingTableBecomesStructuredOnceSeparatorArrivesAndCodeStaysLiteral()
    {
        Assert.IsInstanceOfType<ParagraphBlock>(ResponseMarkdown.Parse("| Name | Value |\n")[0]);
        Assert.IsInstanceOfType<Table>(ResponseMarkdown.Parse("| Name | Value |\n| --- | --- |\n| A | B |")[0]);
        Assert.IsInstanceOfType<FencedCodeBlock>(ResponseMarkdown.Parse("```text\n| A | B |\n| --- | --- |\n```")[0]);
    }
}
