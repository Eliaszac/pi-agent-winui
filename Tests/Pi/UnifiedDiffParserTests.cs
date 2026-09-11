using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class UnifiedDiffParserTests
{
    [TestMethod]
    public void PresentsReplacementWithoutPatchHeaders()
    {
        var diff = UnifiedDiffParser.Parse("--- index.js\n+++ index.js\n@@ -1,1 +1,1 @@\n-console.log(2 + 4);\n+console.log(2 + 3);\n");
        Assert.AreEqual("index.js", diff.FileName); Assert.AreEqual(2, diff.Lines.Count);
        Assert.AreEqual("console.log(2 + 4);", diff.Lines[0].Text);
        Assert.IsTrue(diff.Lines[0].IsRemoved); Assert.AreEqual(1L, diff.Lines[0].OldLine); Assert.IsNull(diff.Lines[0].NewLine);
        Assert.IsTrue(diff.Lines[1].IsAdded); Assert.AreEqual(1L, diff.Lines[1].NewLine); Assert.IsNull(diff.Lines[1].OldLine);
        Assert.AreEqual("", diff.Notice);
    }

    [TestMethod]
    public void PreservesCodeThatResemblesHeadersAndWhitespace()
    {
        var diff = UnifiedDiffParser.Parse("--- a/test.txt\r\n+++ b/test.txt\r\n@@ -4,2 +9,2 @@\r\n--- code\r\n+++ code\r\n \t  keep whitespace\r\n");
        Assert.AreEqual("test.txt", diff.FileName);
        Assert.AreEqual("-- code", diff.Lines[0].Text); Assert.AreEqual("++ code", diff.Lines[1].Text);
        Assert.AreEqual("\t  keep whitespace", diff.Lines[2].Text);
        Assert.AreEqual(5L, diff.Lines[2].OldLine); Assert.AreEqual(10L, diff.Lines[2].NewLine);
    }

    [TestMethod]
    public void SupportsNewFilesDeletedFilesAndMissingFinalNewlines()
    {
        var added = UnifiedDiffParser.Parse("--- /dev/null\n+++ b/new.txt\n@@ -0,0 +1 @@\n+new\n\\ No newline at end of file\n");
        Assert.AreEqual("new.txt", added.FileName); Assert.IsNull(added.Lines[0].OldLine);
        Assert.AreEqual("No newline at end of file", added.Lines[1].Text);
        var deleted = UnifiedDiffParser.Parse("--- a/old.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-old\n");
        Assert.AreEqual("old.txt", deleted.FileName); Assert.IsNull(deleted.Lines[0].NewLine);
    }

    [TestMethod]
    public void SeparatesHunksAndConsecutiveCapturedEdits()
    {
        var patch = "--- a/file\n+++ b/file\n@@ -1 +1 @@\n-old\n+new\n@@ -9 +9 @@\n-a\n+b\n";
        var diff = UnifiedDiffParser.Parse(patch + patch);
        Assert.AreEqual(8, diff.Lines.Count(line => line.IsAdded || line.IsRemoved));
        Assert.IsTrue(diff.Lines.Any(line => line.Kind == 'h'));
        Assert.IsTrue(diff.Lines.Any(line => line.Text == "Next captured edit"));
    }

    [TestMethod]
    public void HandlesIncompleteAndOversizedPatchesWithoutInventingLineNumbers()
    {
        var incomplete = UnifiedDiffParser.Parse("@@ -1,2 +1,2 @@\n first\n");
        Assert.IsTrue(incomplete.Notice.Contains("incomplete"));
        var excessive = UnifiedDiffParser.Parse("@@ -999999999999999999999 +1 @@\n-old\n+new\n");
        Assert.AreEqual(0, excessive.Lines.Count); Assert.IsTrue(excessive.Notice.Length > 0);
        var large = UnifiedDiffParser.Parse("@@ -0,0 +1,3000 @@\n" + string.Concat(Enumerable.Repeat("+line\n", 3000)));
        Assert.AreEqual(UnifiedDiffParser.MaximumLines, large.Lines.Count);
        Assert.IsTrue(large.Notice.Contains("2,000"));
    }
}
