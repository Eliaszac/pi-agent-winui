using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class SidebarLayoutStateTests
{
    [TestMethod]
    public async Task PreferencesSurviveRestartAndTransientNarrowLayout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sidebar-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sidebar = new SidebarLayoutState();
            sidebar.ResizeTo(410);
            sidebar.Toggle();
            var store = new PiAgentGui.Services.Settings.SidebarPreferencesStore(Path.Combine(directory, "sidebar.json"));
            await store.SaveAsync(new(sidebar.PreferredWidth, sidebar.PreferredOpen));
            var saved = await new PiAgentGui.Services.Settings.SidebarPreferencesStore(Path.Combine(directory, "sidebar.json")).LoadAsync();
            var restored = new SidebarLayoutState();
            restored.Restore(saved.Width, saved.IsOpen);
            Assert.IsFalse(restored.IsOpen);
            Assert.AreEqual(410d, restored.ExpandedWidth);
            var changes = 0;
            restored.PreferenceChanged += (_, _) => changes++;
            restored.SetAvailableWidth(350);
            restored.SetAvailableWidth(1200);
            Assert.AreEqual(410d, restored.ExpandedWidth);
            Assert.IsFalse(restored.IsOpen);
            Assert.AreEqual(0, changes);
            restored.Toggle();
            restored.SetAvailableWidth(350);
            restored.SetAvailableWidth(1200);
            Assert.IsTrue(restored.IsOpen);
            Assert.AreEqual(1, changes);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

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
