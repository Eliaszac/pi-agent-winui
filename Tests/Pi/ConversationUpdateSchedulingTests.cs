using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ConversationUpdateSchedulingTests
{
    [TestMethod]
    public async Task InputCanRunBetweenBatchesWithoutDroppingOrReorderingMessages()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        await vm.InitializeAsync();
        dispatcher.Drain();
        for (var index = 0; index < 300; index++)
            session.Emit(new() { Entry = new(index.ToString(), "You", index.ToString(), IsUser: true) });

        dispatcher.DrainOne();
        Assert.IsTrue(vm.Entries.Count > 0 && vm.Entries.Count < 300);
        var delivered = vm.Entries.Count;
        dispatcher.Post(() => vm.Draft = "Typing while updates arrive");
        dispatcher.DrainOne();
        Assert.AreEqual("Typing while updates arrive", vm.Draft);
        Assert.AreEqual(delivered, vm.Entries.Count);

        dispatcher.Drain();
        CollectionAssert.AreEqual(Enumerable.Range(0, 300).Select(index => index.ToString()).ToArray(),
            vm.Entries.Select(entry => entry.Text).ToArray());
    }
}
