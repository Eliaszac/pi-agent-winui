using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class TranscriptPresentationTests
{
    [TestMethod]
    public void WhitespaceAssistantUpdatesDoNotCreateRowsOrSplitToolGroups()
    {
        var projection = new TranscriptPresentation();
        var entries = new List<ChatEntryViewModel>();
        for (var index = 0; index < 4; index++)
        {
            entries.Add(new(new ChatEntry($"a:{index}", "Pi", " \n", Status: "Responding", IsAssistant: true, IsComplete: false)));
            entries.Add(new(new ChatEntry($"t:{index}", "read", "", Status: "Running", IsTool: true)));
        }
        Assert.IsTrue(projection.Project(entries).Single().IsToolGroup);
        var response = new ChatEntryViewModel(new ChatEntry("answer", "Pi", "Hello", Status: "Responding", IsAssistant: true, IsComplete: false));
        Assert.IsFalse(response.HasHeader);
        response.Update(new ChatEntry("answer", "Pi", "Hello", Status: "Failed: disconnected", IsAssistant: true));
        Assert.IsTrue(response.HasHeader);
    }

    [TestMethod]
    public void ProcessingSitsAfterLatestPromptAndDisappearsWhenIdle()
    {
        var projection = new TranscriptPresentation();
        var older = new ChatEntryViewModel(new ChatEntry("old", "", "Old prompt", IsUser: true));
        var prompt = new ChatEntryViewModel(new ChatEntry("user", "", "Current prompt", IsUser: true));
        var response = new ChatEntryViewModel(new ChatEntry("answer", "", "Streaming", IsAssistant: true));
        var rows = projection.Project([older, prompt, response], isRunning: true);
        Assert.AreSame(prompt, rows[1]);
        Assert.IsTrue(rows[2].IsProcessing);
        Assert.IsFalse(rows[2].IsMessage);
        Assert.AreSame(response, rows[3]);
        Assert.IsFalse(projection.Project([older, prompt, response]).Any(row => row.IsProcessing));
    }

    [TestMethod]
    public void ConsecutiveToolsGroupAndKeepExpansionDuringUpdates()
    {
        var projection = new TranscriptPresentation();
        var tools = Enumerable.Range(0, 4).Select(index => new ChatEntryViewModel(
            new ChatEntry($"tool:{index}", "bash", "output", Status: "Completed", IsTool: true))).ToList();
        Assert.IsTrue(projection.Project(tools.Take(2)).Single().IsToolGroup);
        var group = projection.Project(tools).Single();
        Assert.IsTrue(group.IsToolGroup);
        Assert.IsFalse(group.IsExpanded);
        Assert.AreEqual("Called 4 tools", group.GroupSummary);
        group.IsExpanded = true;
        tools.Add(new(new ChatEntry("tool:4", "read", "partial", Status: "Running", IsTool: true)));
        var updated = projection.Project(tools).Single();
        Assert.AreSame(group, updated);
        Assert.IsTrue(updated.IsExpanded);
        Assert.AreEqual("Called 5 tools · running", updated.GroupSummary);
        tools[4].Update(new ChatEntry("tool:4", "read", "failure", Status: "Failed", IsTool: true));
        Assert.AreEqual("Called 5 tools · includes failures", projection.Project(tools).Single().GroupSummary);
    }

    [TestMethod]
    public void VisibleMessagesSeparateToolGroupsButEmptyAssistantToolMessagesDoNot()
    {
        var projection = new TranscriptPresentation();
        var entries = new List<ChatEntryViewModel>();
        for (var index = 0; index < 4; index++)
        {
            entries.Add(new(new ChatEntry($"assistant:{index}", "Pi", "", IsAssistant: true)));
            entries.Add(new(new ChatEntry($"tool:{index}", "read", "result", IsTool: true)));
        }
        var response = new ChatEntryViewModel(new ChatEntry("answer", "Pi", "**Answer**", IsAssistant: true));
        entries.Add(response);
        entries.Add(new(new ChatEntry("last-tool", "read", "last result", IsTool: true)));
        var rows = projection.Project(entries);
        Assert.AreEqual(3, rows.Count);
        Assert.IsTrue(rows[0].IsToolGroup);
        Assert.AreSame(response, rows[1]);
        Assert.IsTrue(rows[2].IsTool);
        Assert.AreEqual("**Answer**", response.Text, "Formatting must not change copy text.");
    }
}
