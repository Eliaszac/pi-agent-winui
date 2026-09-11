using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ComposerInputTests
{
    [TestMethod]
    public async Task MissingProviderRedirectKeepsDraftAndScreenshotUntilExplicitSend()
    {
        var session = new FakeConversationSession(); var dispatcher = new QueuedUiDispatcher();
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync(); dispatcher.Drain();
        var missing = true; var redirects = 0;
        model.TryOpenProviderSetup = () => { if (!missing) return false; redirects++; return true; };
        model.Draft = "Explain this screenshot";
        var screenshot = new PiAgentGui.Models.Conversations.ChatImage("dGVzdA==");
        model.AddScreenshot(screenshot);
        await model.SendCommand.ExecuteAsync();
        Assert.AreEqual(1, redirects);
        Assert.AreEqual(0, session.Sent.Count);
        Assert.AreEqual("Explain this screenshot", model.Draft);
        Assert.AreSame(screenshot, model.PendingImages.Single());
        missing = false;
        Assert.AreEqual(0, session.Sent.Count);
        await model.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.AreEqual(1, session.Sent.Count);
        Assert.AreEqual("", model.Draft);
        Assert.AreEqual(0, model.PendingImages.Count);
    }

    [TestMethod]
    public async Task LocalCommandsRemainAvailableWithoutAProvider()
    {
        var session = new FakeConversationSession(); var dispatcher = new QueuedUiDispatcher();
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync(); dispatcher.Drain();
        var handled = false;
        model.HandleComposerCommand = _ => { handled = true; return Task.FromResult(true); };
        model.TryOpenProviderSetup = () => throw new AssertFailedException("Local commands must not require authentication.");
        model.Draft = "/help";
        await model.SendCommand.ExecuteAsync();
        Assert.IsTrue(handled);
        Assert.IsFalse(model.HasError);
        Assert.AreEqual(0, session.Sent.Count);
    }

    [TestMethod]
    public async Task TypingKeepsActionsCorrectWithoutRefreshingUnchangedQueueState()
    {
        var session = new FakeConversationSession(); var dispatcher = new QueuedUiDispatcher();
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync(); dispatcher.Drain();
        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        model.Draft = "h";
        Assert.IsTrue(model.CanSend);
        CollectionAssert.Contains(changes, nameof(model.CanSend));
        changes.Clear();
        model.Draft = "hello world";
        CollectionAssert.AreEqual(new[] { nameof(model.Draft) }, changes.ToArray());
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        model.Draft = "";
        Assert.IsTrue(model.ComposerShowsStop);
        Assert.AreSame(model.StopCommand, model.ComposerActionCommand);
        changes.Clear();
        model.Draft = "follow up";
        Assert.IsFalse(model.ComposerShowsStop);
        Assert.IsTrue(model.ShowSeparateStop);
        CollectionAssert.Contains(changes, nameof(model.ComposerActionCommand));
        Assert.AreEqual("Queue follow-up", model.ComposerActionLabel);
        model.Draft = "/steer correction";
        Assert.AreEqual("Steer", model.ComposerActionLabel);
        Assert.AreEqual(0, session.Sent.Count);
    }
}
