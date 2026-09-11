using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class DiffPresentationTests
{
    [TestMethod]
    public void RepeatedExpansionRevealsFifteenLinesAndEventuallyRemovesGap()
    {
        var rows = Enumerable.Range(1, 48).Select(number => new DiffLine($"line {number}", number, number, number == 48 ? '+' : ' ')).ToArray();
        var revealed = new HashSet<int>();
        var filtered = DiffContextFilter.ChangesOnly(rows, revealed: revealed);
        Assert.AreEqual(44, filtered.Single(row => row.CanExpand).HiddenCount);
        DiffContextFilter.RevealNext(filtered.Single(row => row.CanExpand), revealed);
        filtered = DiffContextFilter.ChangesOnly(rows, revealed: revealed);
        Assert.AreEqual(29, filtered.Single(row => row.CanExpand).HiddenCount);
        Assert.AreEqual(1L, filtered[0].OldLine);
        DiffContextFilter.RevealNext(filtered.Single(row => row.CanExpand), revealed);
        filtered = DiffContextFilter.ChangesOnly(rows, revealed: revealed);
        Assert.AreEqual(14, filtered.Single(row => row.CanExpand).HiddenCount);
        DiffContextFilter.RevealNext(filtered.Single(row => row.CanExpand), revealed);
        filtered = DiffContextFilter.ChangesOnly(rows, revealed: revealed);
        CollectionAssert.AreEqual(rows, filtered.ToArray());
    }

    [TestMethod]
    public void HistoricalHunkGapsCannotBeExpanded()
    {
        var document = UnifiedDiffParser.Parse("@@ -1 +1 @@\n-old\n+new\n@@ -20 +20 @@\n-before\n+after\n");
        var gap = document.Lines.Single(row => row.Kind == 'h');
        var revealed = new HashSet<int>();
        DiffContextFilter.RevealNext(gap, revealed);
        Assert.IsFalse(gap.CanExpand);
        Assert.AreEqual(0, revealed.Count);
    }

    [TestMethod]
    public void ChangesOnlyKeepsThreeContextLinesAndOriginalNumbers()
    {
        var rows = Enumerable.Range(1, 30).Select(number =>
            new DiffLine($"line {number}", number, number, number is 8 or 23 ? '+' : ' ')).ToArray();
        var filtered = DiffContextFilter.ChangesOnly(rows);
        CollectionAssert.AreEqual(new long[] { 5, 6, 7, 8, 9, 10, 11, 20, 21, 22, 23, 24, 25, 26 },
            filtered.Where(line => line.NewLine.HasValue).Select(line => line.NewLine!.Value).ToArray());
        Assert.AreEqual(3, filtered.Count(line => line.Kind == 'h'));
        Assert.AreEqual(30, rows.Length);
        Assert.AreEqual(1L, rows[0].NewLine);
    }

    [TestMethod]
    public void ChangesOnlyPreservesReplacementAndMissingNewlineNotice()
    {
        var document = UnifiedDiffParser.Parse("@@ -1 +1 @@\n-old\n\\ No newline at end of file\n+new\n");
        CollectionAssert.AreEqual(document.Lines.ToArray(), DiffContextFilter.ChangesOnly(document.Lines).ToArray());
    }

    [TestMethod]
    public void HighlightsIndependentFileStatesWithoutChangingText()
    {
        var document = UnifiedDiffParser.Parse("@@ -1,3 +1,3 @@\n-/*\n+const value = 1;\n middle\n */\n");
        var highlighted = DiffSyntaxHighlighter.Highlight(document, "folder with spaces/index.js");
        foreach (var row in highlighted.Lines)
        {
            if (row.OldTokens is { } old) Assert.AreEqual(row.Text, string.Concat(old.Select(part => part.Text)));
            if (row.NewTokens is { } current) Assert.AreEqual(row.Text, string.Concat(current.Select(part => part.Text)));
        }
        var middle = highlighted.Lines.Single(row => row.Text == "middle");
        Assert.IsTrue(middle.OldTokens!.Any(part => part.Kind == "comment"));
        Assert.IsFalse(middle.NewTokens!.Any(part => part.Kind == "comment"));
        var split = SplitDiffBuilder.Build(highlighted.Lines);
        Assert.AreSame(middle.OldTokens, split.Left.Single(row => row.Text == "middle").SyntaxTokens);
        Assert.AreSame(middle.NewTokens, split.Right.Single(row => row.Text == "middle").SyntaxTokens);
    }

    [TestMethod]
    public void FilteringRetainsHighlightContextFromHiddenLines()
    {
        var patch = "@@ -1,8 +1,8 @@\n /*\n a\n b\n c\n d\n-old\n+new\n e\n */\n";
        var highlighted = DiffSyntaxHighlighter.Highlight(UnifiedDiffParser.Parse(patch), "main.java");
        var filtered = DiffContextFilter.ChangesOnly(highlighted.Lines);
        Assert.IsFalse(filtered.Any(row => row.Text == "/*"));
        Assert.IsTrue(filtered.Single(row => row.Text == "new").SyntaxTokens!.Any(part => part.Kind == "comment"));
    }
}
