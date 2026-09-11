using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class SplitDiffBuilderTests
{
    [TestMethod]
    public void AlignsUnequalReplacementsAndKeepsContextTogether()
    {
        var document = UnifiedDiffParser.Parse("@@ -10,4 +10,3 @@\n before\n-old one\n-old two\n+new\n after\n");
        var split = SplitDiffBuilder.Build(document.Lines);
        Assert.AreEqual(4, split.Left.Count); Assert.AreEqual(split.Left.Count, split.Right.Count);
        Assert.AreEqual("old one", split.Left[1].Text); Assert.AreEqual("new", split.Right[1].Text);
        Assert.AreEqual("old two", split.Left[2].Text); Assert.AreEqual("", split.Right[2].Text);
        Assert.IsNull(split.Right[2].NewLine);
        Assert.AreEqual("after", split.Left[3].Text); Assert.AreEqual("after", split.Right[3].Text);
        Assert.AreEqual(13L, split.Left[3].OldLine); Assert.AreEqual(12L, split.Right[3].NewLine);
    }

    [TestMethod]
    public void DoesNotPairEditsAcrossOmittedRegions()
    {
        var document = UnifiedDiffParser.Parse("@@ -1 +0,0 @@\n-deleted\n@@ -9,0 +9 @@\n+added\n");
        var split = SplitDiffBuilder.Build(document.Lines);
        Assert.AreEqual("deleted", split.Left[0].Text); Assert.AreEqual("", split.Right[0].Text);
        Assert.AreEqual('h', split.Left[1].Kind);
        Assert.AreEqual("", split.Left[2].Text); Assert.AreEqual("added", split.Right[2].Text);
    }

    [TestMethod]
    public void FullViewCanReadBeyondSummaryLimit()
    {
        var patch = "@@ -0,0 +1,3000 @@\n" + string.Concat(Enumerable.Repeat("+text\n", 3000));
        Assert.AreEqual(2000, UnifiedDiffParser.Parse(patch).Lines.Count);
        Assert.AreEqual(3000, UnifiedDiffParser.Parse(patch, 20000).Lines.Count);
    }

    [TestMethod]
    public void MissingNewlineNoticeStaysOnCorrectSideWithoutBreakingReplacementAlignment()
    {
        var document = UnifiedDiffParser.Parse("@@ -1 +1 @@\n-old\n\\ No newline at end of file\n+new\n");
        var split = SplitDiffBuilder.Build(document.Lines);
        Assert.AreEqual("old", split.Left[0].Text); Assert.AreEqual("new", split.Right[0].Text);
        Assert.AreEqual("No newline at end of file", split.Left[1].Text); Assert.AreEqual("", split.Right[1].Text);
    }

    [TestMethod]
    public void NewFilePatchPreservesFinalNewlineAndEmptyFile()
    {
        Assert.AreEqual("", NewFilePatch.Create(""));
        var patch = NewFilePatch.Create("one\ntwo");
        var parsed = UnifiedDiffParser.Parse(patch);
        Assert.AreEqual(2, parsed.Lines.Count(line => line.IsAdded));
        Assert.IsTrue(patch.Contains("No newline"));
        Assert.IsFalse(NewFilePatch.Create("one\n").Contains("No newline"));
    }
}
