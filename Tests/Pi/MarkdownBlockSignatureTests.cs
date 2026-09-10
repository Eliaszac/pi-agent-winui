using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class MarkdownBlockSignatureTests
{
    [TestMethod]
    public void ValidSpanUsesOnlyItsOwnBlock()
    {
        Assert.AreEqual("ParagraphBlock:hello", MarkdownBlockSignature.Create("ParagraphBlock", "hello\nworld", 0, 4));
    }

    [DataTestMethod]
    [DataRow(0, 6)]
    [DataRow(0, int.MaxValue)]
    [DataRow(-1, 3)]
    [DataRow(8, 9)]
    [DataRow(0, -1)]
    public void OutOfRangeSpansUseWholeSnapshotAndKeepLaterChangesVisible(int start, int end)
    {
        Assert.AreEqual("CodeBlock:hello", MarkdownBlockSignature.Create("CodeBlock", "hello", start, end));
        Assert.AreEqual("CodeBlock:other", MarkdownBlockSignature.Create("CodeBlock", "other", start, end));
    }

    [TestMethod]
    public void EveryStreamingPrefixCanBeSignedIncludingUnfinishedBlocks()
    {
        var examples = new[]
        {
            "# Heading\r\n\r\nA **bold** response.\r\n",
            "```csharp\nConsole.WriteLine(\"Hello\");\n```\n",
            "    indented code\n    another line\n",
            "> quoted text\n> continuation\n\n- first\n- second\n",
            "Title\n=====\n\nText [link](https://example.com)\n"
        };
        foreach (var example in examples)
            for (var length = 0; length <= example.Length; length++)
            {
                var source = example[..length];
                foreach (var block in Markdig.Markdown.Parse(source))
                    Assert.IsNotNull(MarkdownBlockSignature.Create(block.GetType().Name, source, block.Span.Start, block.Span.End));
            }
    }

    [TestMethod]
    public void EmptyInputIsSafe()
    {
        Assert.AreEqual("ParagraphBlock:", MarkdownBlockSignature.Create("ParagraphBlock", "", 0, 0));
    }
}
