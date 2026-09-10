using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.Tests.Terminal;

[TestClass]
public sealed class TerminalPanelTests
{
    [TestMethod]
    public async Task ClosingLastTabClosesPanelAndDisposesItsShell()
    {
        var session = new FakeTerminalSession();
        var panel = new TerminalPanelViewModel(_ => session);
        panel.Toggle(@"C:\Project");
        Assert.IsTrue(panel.IsOpen);
        Assert.AreEqual(1, panel.Tabs.Count);
        await panel.CloseAsync(panel.Selected!);
        Assert.IsFalse(panel.IsOpen);
        Assert.IsNull(panel.Selected);
        Assert.IsTrue(session.Disposed);
    }

    [TestMethod]
    public async Task TabsRemainIndependentAndHidingPreservesSessions()
    {
        var sessions = new List<FakeTerminalSession>();
        var panel = new TerminalPanelViewModel(_ => { var shell = new FakeTerminalSession(); sessions.Add(shell); return shell; });
        panel.Add(@"C:\First");
        var first = panel.Selected!;
        panel.Add(@"C:\Second");
        var second = panel.Selected!;
        Assert.AreEqual(@"C:\First", first.Directory);
        Assert.AreEqual(@"C:\Second", second.Directory);
        panel.Toggle(@"C:\Third");
        Assert.IsFalse(panel.IsOpen);
        Assert.IsFalse(sessions.Any(session => session.Disposed));
        panel.Toggle(@"C:\Third");
        Assert.AreEqual(2, panel.Tabs.Count);
        Assert.AreSame(second, panel.Selected);
        await panel.CloseAsync(second);
        Assert.IsTrue(panel.IsOpen);
        Assert.AreSame(first, panel.Selected);
        Assert.IsTrue(sessions[1].Disposed);
        Assert.IsFalse(sessions[0].Disposed);
        await panel.DisposeAsync();
        Assert.IsTrue(sessions.All(session => session.Disposed));
    }
}
