using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class CheckpointTests
{
    [TestMethod]
    public void ReadOnlyGitRefreshDoesNotInvalidateCheckpointAttribution()
    {
        Assert.IsFalse(GitWorkspaceActivity.MayWrite(["status", "--porcelain=v1"]));
        Assert.IsFalse(GitWorkspaceActivity.MayWrite(["diff", "--no-ext-diff", "--no-textconv", "--numstat"]));
        Assert.IsTrue(GitWorkspaceActivity.MayWrite(["diff", "--no-ext-diff", "--no-textconv", "--output=result.patch"]));
        Assert.IsTrue(GitWorkspaceActivity.MayWrite(["restore", "--worktree", "--", "file"]));
        Assert.IsTrue(GitWorkspaceActivity.MayWrite(["commit", "-m", "message"]));
    }

    private static JsonElement Packet(string id, string state = "complete", string applied = "[]") => JsonDocument.Parse($$$"""
        {"version":1,"manifest":{"id":"{{{id}}}","response":"assistant:123","state":"{{{state}}}","overlap":false,"omitted":0,
        "files":[{"path":"new.txt","kind":"created","patch":"","added":0,"removed":0},{"path":"old.txt","kind":"deleted","patch":null,"added":0,"removed":3}],"applied":{{{applied}}}}}
        """).RootElement.Clone();

    [TestMethod]
    public void ManifestValidatesIdentityAndKinds()
    {
        var id = Guid.NewGuid().ToString("N");
        var parsed = CheckpointManifestParser.Parse(Packet(id).GetProperty("manifest"));
        Assert.IsNotNull(parsed);
        Assert.AreEqual("created", parsed.Files[0].Kind);
        Assert.AreEqual("deleted", parsed.Files[1].Kind);
        Assert.IsNull(CheckpointManifestParser.Parse(Packet("../invalid").GetProperty("manifest")));
    }

    [TestMethod]
    public async Task ManifestBeforeHistoryAttachesToExactResponseAndSupportsUndo()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        var id = Guid.NewGuid().ToString("N");
        session.Emit(new() { Checkpoint = Packet(id) });
        session.Emit(new() { IsConnected = true, History = [new("user:1", "You", "change", IsUser: true), new("assistant:123", "Pi", "done", IsAssistant: true)] });
        dispatcher.Drain();
        Assert.AreEqual(id, vm.Entries.Last().Summary?.CheckpointId);
        Assert.AreEqual("Created", vm.Entries.Last().Summary!.Files.First().Kind);
        session.Emit(new() { Checkpoint = Packet(id, "reverted", "[\"new.txt\"]") });
        dispatcher.Drain();
        Assert.IsTrue(vm.Entries.Last().Summary!.CanUndoRevert);
        Assert.AreEqual("", vm.Draft);
    }

    [TestMethod]
    public async Task NoCheckpointDoesNotPrepareAgentPrompt()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher);
        session.Emit(new() { IsConnected = true });
        dispatcher.Drain();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => vm.CheckpointOperationAsync("preview", new RunChangesViewModel([])));
        Assert.AreEqual("", vm.Draft);
    }

    [TestMethod]
    public void CaseSensitiveTargetsKeepDistinctFileNames()
    {
        var summary = new RunChangesViewModel([new("a", "", 0, 0, null, "created"), new("A", "", 0, 0, null, "deleted")], caseSensitive: true);
        Assert.AreEqual(2, summary.Files.Count);
    }
}
