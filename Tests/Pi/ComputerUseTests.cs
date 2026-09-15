using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.Extensions;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ComputerUseTests
{
    [TestMethod]
    public async Task ActivityTracksConcurrentToolsAndStopUsesExistingRunCancellation()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsConnected = true, IsRunning = true });
        session.Emit(new() { Entry = new("tool:1", "observe_ui", "", Status: "Running", IsTool: true) });
        session.Emit(new() { Entry = new("tool:2", "act_ui", "", Status: "Running", IsTool: true) });
        dispatcher.Drain();
        Assert.IsTrue(vm.IsComputerUseActive);
        session.Emit(new() { Entry = new("tool:1", "observe_ui", "done", Status: "Completed", IsTool: true) });
        dispatcher.Drain();
        Assert.IsTrue(vm.IsComputerUseActive);
        await vm.StopCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.IsFalse(vm.IsComputerUseActive);
        session.Emit(new() { IsRunning = true });
        dispatcher.Drain();
        Assert.IsFalse(vm.IsComputerUseActive);
    }

    [TestMethod]
    public async Task HistoryAndOrdinaryToolsDoNotShowComputerActivity()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsConnected = true, History = [new("old", "act_ui", "", Status: "Running", IsTool: true)] });
        session.Emit(new() { IsRunning = true, Entry = new("bash", "bash", "", Status: "Running", IsTool: true) });
        dispatcher.Drain();
        Assert.IsFalse(vm.IsComputerUseActive);
        session.Emit(new() { Entry = new("active", "wait_for", "", Status: "Running", IsTool: true) });
        dispatcher.Drain();
        Assert.IsTrue(vm.IsComputerUseActive);
        session.Emit(new() { Entry = new("active", "wait_for", "error", Status: "Failed", IsTool: true) });
        dispatcher.Drain();
        Assert.IsFalse(vm.IsComputerUseActive);
    }

    [TestMethod]
    public void ComputerUseIsThirdPartyOptInWithPinnedSetup()
    {
        Assert.IsFalse(SupportedExtensions.ComputerUse.Bundled);
        Assert.IsNull(SupportedExtensions.ComputerUse.WriteEnabled);
        StringAssert.Contains(SupportedExtensions.ComputerUse.InstallCommand, ComputerUseSupport.Package + "@" + ComputerUseSupport.Version);
        Assert.IsTrue(SupportedExtensions.All.Contains(SupportedExtensions.ComputerUse));
    }
}
