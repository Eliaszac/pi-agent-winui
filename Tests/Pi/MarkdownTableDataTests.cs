using Markdig.Extensions.Tables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;
using System.Text.Json;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class MarkdownTableDataTests
{
    private static MarkdownTableData Parse(string markdown) => new((Table)ResponseMarkdown.Parse(markdown)[0]);

    [TestMethod]
    public void NumericSortTogglesKeepsTiesStableAndBlanksLast()
    {
        var data = Parse("| Value |\n| --- |\n| 10 |\n| 2 |\n| |\n| 2.0 |\n| -4 |\n| 1,000 |");
        data.Sort(0);
        Assert.AreEqual("number", data.SortType);
        CollectionAssert.AreEqual(new[] { 4, 1, 3, 0, 5, 2 }, data.Order.ToArray());
        data.Sort(0);
        Assert.IsTrue(data.Descending);
        CollectionAssert.AreEqual(new[] { 5, 0, 1, 3, 4, 2 }, data.Order.ToArray());
        data.Reset();
        Assert.IsNull(data.SortColumn);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, data.Order.ToArray());
    }

    [TestMethod]
    public void DatesSortChronologicallyAndAmbiguousDatesFallBackToText()
    {
        var data = Parse("| Date |\n| --- |\n| January 2, 2025 |\n| 2024-12-31 |\n| 3 Jan 2025 |");
        data.Sort(0);
        Assert.AreEqual("date", data.SortType);
        CollectionAssert.AreEqual(new[] { 1, 0, 2 }, data.Order.ToArray());
        data.Sort(0);
        CollectionAssert.AreEqual(new[] { 2, 0, 1 }, data.Order.ToArray());
        var ambiguous = Parse("| Date |\n| --- |\n| 03/04/2025 |\n| 2025-01-01 |");
        ambiguous.Sort(0);
        Assert.AreEqual("text", ambiguous.SortType);
    }

    [TestMethod]
    public void SortUsesVisibleTextAndChangingColumnStartsAscending()
    {
        var data = Parse("| Name | Count |\n| --- | --- |\n| **zebra** | 1 |\n| [Apple](https://example.com/z) | 2 |\n| `apple` | 3 |");
        data.Sort(0);
        CollectionAssert.AreEqual(new[] { 1, 2, 0 }, data.Order.ToArray());
        data.Sort(0);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, data.Order.ToArray());
        data.Sort(1);
        Assert.IsFalse(data.Descending);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, data.Order.ToArray());
    }

    [TestMethod]
    public void ExportsContainAllRowsInSortedOrderAndUniqueJsonKeys()
    {
        var markdown = "| Name | Name | | Name (2) |\n| --- | --- | --- | --- |\n" +
            string.Join('\n', Enumerable.Range(1, 12).Select(i => $"| {i} | **value, \"{i}\"** | x | y |"));
        var data = Parse(markdown);
        data.Sort(0); data.Sort(0);
        using var json = JsonDocument.Parse(data.ExportJson());
        Assert.AreEqual(12, json.RootElement.GetArrayLength());
        var first = json.RootElement[0];
        Assert.AreEqual(4, first.EnumerateObject().Count());
        Assert.AreEqual("12", first.GetProperty("Name").GetString());
        Assert.AreEqual("value, \"12\"", first.GetProperty("Name (2)").GetString());
        Assert.AreEqual("x", first.GetProperty("Column 3").GetString());
        Assert.AreEqual("y", first.GetProperty("Name (2) (2)").GetString());
        StringAssert.Contains(data.ExportCsv(), "\"12\",\"value, \"\"12\"\"\",\"x\",\"y\"\r\n");
        Assert.IsFalse(data.ExportCsv().Contains("**"));
    }

    [TestMethod]
    public void MixedNumbersAndTextNeverPartiallyCoerceColumn()
    {
        var data = Parse("| Value |\n| --- |\n| 20 |\n| 3 |\n| n/a |\n| 1,2 |");
        data.Sort(0);
        Assert.AreEqual("text", data.SortType);
        CollectionAssert.AreEqual(new[] { 3, 0, 1, 2 }, data.Order.ToArray());
    }
}
