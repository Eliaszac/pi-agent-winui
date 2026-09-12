using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.Tests.Terminal;

[TestClass]
public sealed class SidePanelTests
{
    [TestMethod]
    public void TabsAreUniqueExceptTerminalsAndRetainOrderSelectionAndNames()
    {
        var first = new SidePanelState();
        var second = new SidePanelState();
        var files = first.Open("files", "Files");
        Assert.AreSame(files, first.Open("files", "Files"));
        var shell1 = new TerminalTabViewModel("project", 1, new FakeTerminalSession());
        var shell2 = new TerminalTabViewModel("project", 2, new FakeTerminalSession());
        var terminal1 = first.Open("terminal", "Terminal 1", shell1);
        var terminal2 = first.Open("terminal", "Terminal 2", shell2);
        Assert.AreSame(terminal1, first.Open("terminal", "Terminal 1", shell1));
        terminal1.Title = " Server ";
        first.Tabs.Move(1, 0);
        second.Open("files", "Files");
        first.IsOpen = false;
        Assert.AreEqual(3, first.Tabs.Count);
        Assert.AreSame(terminal1, first.Selected);
        Assert.AreSame(terminal1, first.Tabs[0]);
        Assert.AreEqual("Server", terminal1.Title);
        Assert.AreEqual(1, second.Tabs.Count);
        first.Close(terminal1);
        Assert.AreSame(files, first.Selected);
        first.Close(files); first.Close(terminal2);
        Assert.IsNull(first.Selected);
        Assert.AreEqual(0, first.Tabs.Count);
    }

    [TestMethod]
    public async Task ConversationsSharingAProjectNeverReuseOrCloseEachOthersShells()
    {
        var sessions = new List<FakeTerminalSession>();
        await using var terminals = new TerminalPanelViewModel(_ => { var session = new FakeTerminalSession(); sessions.Add(session); return session; });
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        terminals.ConversationId = first;
        terminals.Add("project"); var firstTab = terminals.Selected!;
        terminals.Hide();
        terminals.ConversationId = second;
        terminals.Toggle("project"); var secondTab = terminals.Selected!;
        Assert.AreNotSame(firstTab, secondTab);
        Assert.AreEqual(2, terminals.Tabs.Count);
        terminals.Hide();
        terminals.ConversationId = first;
        terminals.Show(firstTab);
        Assert.AreSame(firstTab, terminals.Selected);
        Assert.IsFalse(sessions.Any(session => session.Disposed));
        await terminals.CloseAsync(firstTab);
        Assert.IsTrue(sessions[0].Disposed);
        Assert.IsFalse(sessions[1].Disposed);
        Assert.IsNull(terminals.Selected);
        Assert.IsFalse(terminals.IsOpen);
    }
}
