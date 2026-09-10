using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class SidebarLayoutStateTests
{
    [TestMethod]
    public void DragCollapseKeepsGripReachableAndCanExpandAgain()
    {
        var sidebar = new SidebarLayoutState();
        sidebar.ResizeTo(340);
        sidebar.ResizeTo(100);
        Assert.IsFalse(sidebar.IsOpen);
        Assert.AreEqual(48d, sidebar.ContentWidth);
        Assert.AreEqual(340d, sidebar.ExpandedWidth);
        sidebar.Toggle();
        Assert.AreEqual(340d, sidebar.ContentWidth);
        sidebar.Toggle();
        sidebar.ResizeTo(160);
        Assert.IsTrue(sidebar.IsOpen);
        Assert.AreEqual(220d, sidebar.ContentWidth);
    }

    [TestMethod]
    public void NarrowWindowsUseOverlayAndConstrainWidth()
    {
        var sidebar = new SidebarLayoutState();
        sidebar.SetAvailableWidth(400);
        Assert.IsTrue(sidebar.IsOverlay);
        Assert.IsFalse(sidebar.IsOpen);
        sidebar.ResizeTo(600);
        Assert.AreEqual(352d, sidebar.ExpandedWidth);
        sidebar.SetAvailableWidth(1200);
        Assert.IsFalse(sidebar.IsOverlay);
        sidebar.ResizeTo(600);
        Assert.AreEqual(420d, sidebar.ExpandedWidth);
    }
}
