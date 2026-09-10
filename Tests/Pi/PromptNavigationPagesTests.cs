using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PromptNavigationPagesTests
{
    [TestMethod]
    public void FiftyPromptsFitAndEveryOlderPromptRemainsReachable()
    {
        var pages = new PromptNavigationPages();
        pages.Update(50);
        Assert.AreEqual(50, pages.VisibleCount);
        Assert.IsFalse(pages.HasPrevious || pages.HasNext);
        pages.Update(121);
        Assert.AreEqual(100, pages.Start);
        Assert.AreEqual(21, pages.VisibleCount);
        pages.Previous();
        Assert.AreEqual(50, pages.Start);
        Assert.AreEqual(50, pages.VisibleCount);
        pages.Previous();
        pages.Previous();
        Assert.AreEqual(0, pages.Start);
        Assert.IsTrue(pages.HasNext);
    }

    [TestMethod]
    public void NewPromptsDoNotMoveAnOlderPageAndShrinkingHistoryClampsSelection()
    {
        var pages = new PromptNavigationPages();
        pages.Update(101);
        pages.Previous();
        pages.Update(160);
        Assert.AreEqual(1, pages.Page);
        pages.Next();
        pages.Next();
        pages.Update(201);
        Assert.AreEqual(4, pages.Page);
        pages.Update(30);
        Assert.AreEqual(0, pages.Page);
        Assert.AreEqual(30, pages.VisibleCount);
        pages.Update(0);
        Assert.AreEqual(0, pages.VisibleCount);
    }
}
