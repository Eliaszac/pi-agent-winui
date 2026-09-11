using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class RunRevertTests
{
    private static ChatEntry[] Run(string id) =>
    [
        new(id + "-user", "user", "Change " + id, IsUser: true),
        new(id + "-tool", "edit", "Edited", Details: "tool evidence " + id, IsTool: true,
            FileChange: new(id + ".cs", "-old\n+new", 1, 1, null)),
        new(id + "-assistant", "assistant", "Done " + id, IsAssistant: true)
    ];

    [TestMethod]
    public async Task OlderSummaryPreparesOnlyThatRunWithoutSendingOrChangingFiles()
    {
        var session = new FakeConversationSession(); var dispatcher = new QueuedUiDispatcher();
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync(); dispatcher.Drain();
        session.Emit(new() { History = Run("first").Concat(Run("second")).ToArray(), IsRunning = false }); dispatcher.Drain();
        var summary = model.Entries.Single(entry => entry.Id == "first-assistant").Summary!;
        Assert.IsNotNull(summary);
        Assert.IsTrue(model.PrepareRunRevert(summary));
        StringAssert.Contains(model.Draft, "first-user");
        StringAssert.Contains(model.Draft, "first-assistant");
        StringAssert.Contains(model.Draft, "tool evidence first");
        Assert.IsFalse(model.Draft.Contains("second"));
        Assert.AreEqual(0, session.Sent.Count);
        Assert.AreEqual(0, session.Steered.Count);
        model.Draft = "My unfinished draft";
        Assert.IsFalse(model.PrepareRunRevert(summary));
        Assert.AreEqual("My unfinished draft", model.Draft);
        model.Draft = "";
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        Assert.IsFalse(model.PrepareRunRevert(summary));
        Assert.AreEqual("", model.Draft);
    }

    [TestMethod]
    public void EvidenceIsBoundedAndTruncationIsExplicit()
    {
        var run = Run("large").ToList();
        for (var i = 0; i < 100; i++) run.Insert(1, new("tool" + i, "bash", new string('x', 12000), IsTool: true));
        var prompt = RunRevertPrompt.Build(run);
        Assert.IsTrue(prompt.Length < 65000);
        StringAssert.Contains(prompt, "truncated");
        StringAssert.Contains(prompt, "Remaining evidence omitted");
        StringAssert.Contains(prompt, "Leave commits, branches and staging untouched");
    }
}
