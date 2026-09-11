using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PromptQueueTests
{
    [TestMethod]
    public async Task QueuedAttachmentsAndFileReferencesStayWithTheirConversation()
    {
        var dispatcher = new QueuedUiDispatcher();
        var firstSession = new FakeConversationSession();
        await using var first = new ConversationViewModel(firstSession, dispatcher);
        await using var second = new ConversationViewModel(new FakeConversationSession(), dispatcher);
        await first.InitializeAsync(); await second.InitializeAsync(); dispatcher.Drain();
        firstSession.Emit(new() { IsRunning = true }); dispatcher.Drain();
        var image = new PiAgentGui.Models.Conversations.ChatImage("aGVsbG8=");
        var label = first.FileReferences.Add("C:\\Outside\\example.cs");
        first.Draft = "Read @" + label;
        first.AddScreenshot(image);
        await first.SendCommand.ExecuteAsync();
        Assert.IsFalse(first.HasPendingImages); Assert.IsFalse(second.HasQueuedPrompt);
        await first.EditQueuedCommand.ExecuteAsync();
        Assert.AreEqual("Read @example.cs", first.Draft);
        Assert.AreEqual(image, first.PendingImages.Single());
        StringAssert.Contains(first.FileReferences.Expand(first.Draft), "C:/Outside/example.cs");
    }

    [TestMethod]
    public async Task QueueHasOneSlotAndOnlyDispatchesWhenSettled()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        await vm.InitializeAsync(); dispatcher.Drain();
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        vm.Draft = "Follow up"; await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.IsTrue(vm.HasQueuedPrompt); Assert.AreEqual("", vm.Draft); Assert.AreEqual(0, session.Sent.Count);
        vm.Draft = "Don't overwrite"; await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.AreEqual("Don't overwrite", vm.Draft); Assert.AreEqual("Follow up", vm.QueuedPreview);
        // A successful steering submission clears the validation notice without using the follow-up slot.
        vm.Draft = "/steer Use a smaller change"; await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        CollectionAssert.AreEqual(new[] { "Use a smaller change" }, session.Steered);
        session.Emit(new() { Status = "Finishing…" }); dispatcher.Drain();
        Assert.AreEqual(0, session.Sent.Count);
        session.Emit(new() { IsRunning = false, TurnCompleted = true }); dispatcher.Drain();
        for (var i = 0; i < 100 && vm.HasQueuedPrompt; i++) { await Task.Delay(10); dispatcher.Drain(); }
        Assert.IsFalse(vm.HasQueuedPrompt);
        CollectionAssert.AreEqual(new[] { "Follow up" }, session.Sent);
        session.Emit(new() { IsRunning = false, TurnCompleted = true }); dispatcher.Drain();
        Assert.AreEqual(1, session.Sent.Count);
    }

    [TestMethod]
    public async Task StopHoldsQueueAndEditNeverOverwritesDraft()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        await vm.InitializeAsync(); dispatcher.Drain();
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        vm.Draft = "Later"; await vm.SendCommand.ExecuteAsync();
        await vm.StopCommand.ExecuteAsync(); dispatcher.Drain();
        session.Emit(new() { IsRunning = false, TurnCompleted = true }); dispatcher.Drain();
        Assert.IsTrue(vm.HasQueuedPrompt); Assert.AreEqual("Follow-up paused", vm.QueueLabel); Assert.AreEqual(0, session.Sent.Count);
        vm.Draft = "Keep me"; await vm.EditQueuedCommand.ExecuteAsync();
        Assert.AreEqual("Keep me", vm.Draft); Assert.IsTrue(vm.HasQueuedPrompt);
        vm.Draft = ""; await vm.EditQueuedCommand.ExecuteAsync();
        Assert.AreEqual("Later", vm.Draft); Assert.IsFalse(vm.HasQueuedPrompt);
    }

    [TestMethod]
    public async Task IdleSteerSendsNormallyAndFailedSteerPreservesDraft()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        await vm.InitializeAsync(); dispatcher.Drain();
        vm.Draft = "/steer Hello"; await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        CollectionAssert.AreEqual(new[] { "Hello" }, session.Sent);
        session.SendError = new IOException("Disconnected");
        vm.Draft = "/steer Keep it simple"; await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.AreEqual("/steer Keep it simple", vm.Draft);
        Assert.AreEqual(0, session.Steered.Count);
    }

    [TestMethod]
    public async Task RejectedFollowUpIsHeldWithoutAutomaticReplay()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        await vm.InitializeAsync(); dispatcher.Drain();
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        vm.Draft = "Keep this"; await vm.SendCommand.ExecuteAsync();
        session.SendError = new IOException("Disconnected");
        session.Emit(new() { IsRunning = false, TurnCompleted = true }); dispatcher.Drain();
        for (var i = 0; i < 100 && vm.QueueLabel != "Follow-up paused"; i++) { await Task.Delay(10); dispatcher.Drain(); }
        Assert.IsTrue(vm.HasQueuedPrompt); Assert.AreEqual("Follow-up paused", vm.QueueLabel);
        session.SendError = null;
        session.Emit(new() { IsRunning = false, TurnCompleted = true }); dispatcher.Drain();
        Assert.AreEqual(0, session.Sent.Count);
    }
}
