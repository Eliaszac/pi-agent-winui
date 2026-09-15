using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Tests.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Conversations;

[TestClass]
public sealed class ArtifactUploadTests
{
    private string root = null!;
    private ArtifactStore store = null!;
    private ArtifactPanelViewModel panel = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "pi-upload-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        store = new(Path.Combine(root, "session.artifacts"));
        panel = new(store, new ExecutionTarget { Id = Guid.NewGuid(), Name = "Local", Path = root });
    }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private async Task<string> SourceAsync(string name, string text)
    {
        var path = Path.Combine(root, name);
        await File.WriteAllTextAsync(path, text);
        return path;
    }

    [TestMethod]
    public async Task DiscardPreservesSharedUploads()
    {
        var item = await panel.UploadAsync(await SourceAsync("kept.txt", "Keep"));
        await store.ShareAsync([item.Id], default);
        await store.DiscardUploadAsync(item.Id);
        Assert.IsTrue(File.Exists(await store.GetPathAsync(item.Id)));
    }

    [TestMethod]
    public async Task ArtifactLinksResolveAndOpenLocallyForRemoteConversations()
    {
        var item = await panel.UploadAsync(await SourceAsync("report.txt", "Report"));
        var path = await store.GetPathAsync(item.Id);
        ExecutionTarget? openedTarget = null;
        string? openedPath = null;
        using var links = new PiAgentGui.Services.Files.WorkspaceFileLinks(
            new ExecutionTarget { Id = Guid.NewGuid(), Name = "Remote", Kind = "ssh", Path = "/workspace" },
            (target, file, _, _) => { openedTarget = target; openedPath = file; return Task.CompletedTask; }) { Artifacts = store };
        var mention = new FileMention(0, 10, "report.txt", "report.txt", null, null);
        var matches = await links.ResolveAsync(mention, default);
        Assert.AreEqual(path, matches.Single());
        await links.OpenAsync(matches.Single(), mention, default);
        Assert.IsTrue(openedTarget!.IsLocal);
        Assert.AreEqual(path, openedPath);
    }

    [TestMethod]
    public async Task UploadsAreIndependentAndDoNotOverwriteSameNamedArtifacts()
    {
        var path = await SourceAsync("notes.md", "Original");
        var first = await panel.UploadAsync(path);
        await File.WriteAllTextAsync(path, "Changed source");
        var second = await panel.UploadAsync(path);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual("notes (2).md", second.Name);
        Assert.AreEqual("Original", await File.ReadAllTextAsync(await store.GetPathAsync(first.Id)));
        await panel.DeleteAsync(first.Id);
        Assert.AreEqual("Changed source", await File.ReadAllTextAsync(path));
        Assert.IsTrue(second.Available);
    }

    [TestMethod]
    public async Task UnsentUploadsStayOutOfAgentToolsUntilMessageSubmission()
    {
        var item = await panel.UploadAsync(await SourceAsync("private-draft.txt", "Draft"));
        using var list = JsonDocument.Parse("""{"action":"list"}""");
        using var read = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "read", artifactId = item.Id }));
        Assert.AreEqual(0, (await panel.HandleAsync(list.RootElement))["artifacts"]!.AsArray().Count);
        await Assert.ThrowsExceptionAsync<IOException>(() => panel.HandleAsync(read.RootElement));
        using var save = JsonDocument.Parse("""{"action":"save","name":"private-draft.txt","content":"Overwrite"}""");
        await Assert.ThrowsExceptionAsync<IOException>(() => panel.HandleAsync(save.RootElement));
        await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveAsync(item.Name, [1], "text/plain", "Agent", requireSharedExisting: true));
        await panel.ShareAsync([item.Id]);
        Assert.AreEqual(1, (await panel.HandleAsync(list.RootElement))["artifacts"]!.AsArray().Count);
        Assert.AreEqual("Draft", (await panel.HandleAsync(read.RootElement))["text"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task PromptReferencesSurviveHistoryWithoutDisplayingTransportMetadata()
    {
        var item = await panel.UploadAsync(await SourceAsync("notes.md", "File contents"));
        var prompt = ArtifactPrompt.Append("Review this", [item.Record]);
        CollectionAssert.AreEqual(new[] { item.Id }, ArtifactPrompt.Read(prompt).ToArray());
        Assert.AreEqual("Review this", new ChatEntryViewModel(new("user", "You", prompt, IsUser: true)).Text);
        StringAssert.Contains(prompt, "artifact_read");
        Assert.IsFalse(prompt.Contains("File contents"));
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher) { Artifacts = panel };
        session.Emit(new() { History = [new("user", "You", prompt, IsUser: true)] }); dispatcher.Drain();
        Assert.AreSame(item, workspace.Entries.Single().AttachedFiles.Single());
        Assert.IsTrue(workspace.Entries.Single().HasAttachedFiles);
    }

    [TestMethod]
    public async Task OlderArtifactRecordsRemainSharedAndFileCountLimitHasNoPartialSideEffects()
    {
        var record = await store.SaveAsync("existing.txt", [1], "text/plain", "Agent");
        var metadata = Path.Combine(store.DirectoryPath, record.Id.ToString("N") + ".json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(metadata))!.AsObject();
        json.Remove("Shared");
        await File.WriteAllTextAsync(metadata, json.ToJsonString());
        Assert.IsTrue((await store.ListAsync()).Single().Shared);
        await using var workspace = new ConversationViewModel(new FakeConversationSession(), new QueuedUiDispatcher()) { Artifacts = panel };
        var source = await SourceAsync("too-many.txt", "Hello");
        await Assert.ThrowsExceptionAsync<IOException>(() => workspace.UploadFilesAsync(Enumerable.Repeat(source, 9).ToArray()));
        Assert.IsFalse(workspace.HasPendingFiles);
        Assert.AreEqual(1, (await store.ListAsync()).Count);
    }

    [TestMethod]
    public async Task FileOnlySendAndFailureRecoveryKeepTheRightAttachments()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher) { Artifacts = panel };
        await workspace.InitializeAsync(); dispatcher.Drain();
        await workspace.UploadFilesAsync([await SourceAsync("notes.txt", "Hello")]);
        var item = workspace.PendingFiles.Single();
        Assert.IsTrue(workspace.CanSend);
        session.SendError = new IOException("Offline");
        await workspace.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.AreSame(item, workspace.PendingFiles.Single());
        session.SendError = null;
        await workspace.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.IsFalse(workspace.HasPendingFiles);
        CollectionAssert.AreEqual(new[] { item.Id }, ArtifactPrompt.Read(session.Sent.Single()).ToArray());
        Assert.AreEqual("", ArtifactPrompt.Display(session.Sent.Single()));
        Assert.AreEqual(1, panel.Items.Count);
    }

    [TestMethod]
    public async Task QueueEditingAndSteeringRetainFilesWithoutAffectingOtherConversations()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher) { Artifacts = panel };
        await using var other = new ConversationViewModel(new FakeConversationSession(), dispatcher);
        await workspace.InitializeAsync(); dispatcher.Drain();
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        await workspace.UploadFilesAsync([await SourceAsync("notes.txt", "Hello")]);
        var item = workspace.PendingFiles.Single();
        workspace.Draft = "Review";
        await workspace.SendCommand.ExecuteAsync();
        Assert.IsTrue(workspace.HasQueuedPrompt);
        Assert.IsFalse(workspace.HasPendingFiles);
        Assert.IsFalse(other.HasPendingFiles || other.HasQueuedPrompt);
        await workspace.EditQueuedCommand.ExecuteAsync();
        Assert.AreSame(item, workspace.PendingFiles.Single());
        workspace.Draft = "/steer Use this file";
        await workspace.SendCommand.ExecuteAsync();
        CollectionAssert.AreEqual(new[] { item.Id }, ArtifactPrompt.Read(session.Steered.Single()).ToArray());
        Assert.IsFalse(workspace.HasPendingFiles);
    }

    [TestMethod]
    public async Task RemovingUnsentChipDiscardsCopyAndDeletedAttachmentsBlockSending()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher) { Artifacts = panel };
        await workspace.InitializeAsync(); dispatcher.Drain();
        await workspace.UploadFilesAsync([await SourceAsync("notes.txt", "Hello")]);
        var item = workspace.PendingFiles.Single();
        var path = await store.GetPathAsync(item.Id);
        await workspace.RemoveFileAsync(item);
        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(0, panel.Items.Count);
        Assert.AreEqual(0, (await store.ListAsync()).Count);
        await workspace.UploadFilesAsync([await SourceAsync("notes.txt", "Hello")]);
        item = workspace.PendingFiles.Single();
        workspace.AttachArtifact(item);
        await panel.DeleteAsync(item.Id);
        await workspace.SendCommand.ExecuteAsync();
        Assert.AreEqual(0, session.Sent.Count);
        Assert.IsTrue(workspace.HasPendingFiles);
        StringAssert.Contains(workspace.Error, "deleted");
    }

    [TestMethod]
    public async Task TextReadsArePagedAndPreserveUnicodeBoundaries()
    {
        var item = await panel.UploadAsync(await SourceAsync("notes.txt", "A😀Bæøå"));
        await panel.ShareAsync([item.Id]);
        using var first = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "read", artifactId = item.Id, offset = 0, limit = 2 }));
        var result = await panel.HandleAsync(first.RootElement);
        Assert.AreEqual("A😀", result["text"]!.GetValue<string>());
        Assert.IsTrue(result["hasMore"]!.GetValue<bool>());
        using var next = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "read", artifactId = item.Id, offset = result["nextOffset"]!.GetValue<int>(), limit = 10 }));
        Assert.AreEqual("Bæøå", (await panel.HandleAsync(next.RootElement))["text"]!.GetValue<string>());
        using var foreign = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "read", artifactId = Guid.NewGuid() }));
        await Assert.ThrowsExceptionAsync<IOException>(() => panel.HandleAsync(foreign.RootElement));
    }

    [TestMethod]
    public async Task BinaryReadsOfferTargetAccessAndLocalPathsReferToManagedCopies()
    {
        var path = Path.Combine(root, "data.bin");
        await File.WriteAllBytesAsync(path, [0, 255, 254, 0]);
        var item = await panel.UploadAsync(path);
        using var read = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "read", artifactId = item.Id }));
        await panel.ShareAsync([item.Id]);
        await Assert.ThrowsExceptionAsync<IOException>(() => panel.HandleAsync(read.RootElement));
        using var access = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "path", artifactId = item.Id }));
        var result = (await panel.HandleAsync(access.RootElement))["path"]!.GetValue<string>();
        Assert.AreNotEqual(path, result);
        StringAssert.StartsWith(result, store.DirectoryPath);
        Assert.IsFalse(File.Exists(store.DirectoryPath + ".remote"));
    }

    [TestMethod]
    public async Task ImageTypeUsesSignatureAndTransferInventoryTracksOnlyMaterializedFiles()
    {
        var path = Path.Combine(root, "photo.dat");
        await File.WriteAllBytesAsync(path, [137, 80, 78, 71, 13, 10, 26, 10]);
        var item = await panel.UploadAsync(path);
        Assert.AreEqual("image/png", item.Record.MimeType);
        await panel.ShareAsync([item.Id]);
        using var request = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "read", artifactId = item.Id }));
        Assert.AreEqual("image/png", (await panel.HandleAsync(request.RootElement))["mimeType"]!.GetValue<string>());
        await store.MarkRemoteAsync(item.Id, default);
        Assert.IsTrue((await new ArtifactStore(store.DirectoryPath).RemoteIdsAsync(default)).Contains(item.Id));
        await store.ForgetRemoteAsync(item.Id, default);
        Assert.AreEqual(0, (await store.RemoteIdsAsync(default)).Count);
    }

    [TestMethod]
    public void RemoteCommandsConfineCopiesAndQuoteFilenames()
    {
        var record = new ArtifactRecord(Guid.NewGuid(), "report ' & notes.pdf", "application/pdf", 20, default, default, Guid.NewGuid(), "Uploaded");
        var key = ArtifactTargetStorage.OwnerKey(store.DirectoryPath);
        var command = ArtifactTargetStorage.SaveCommand(key, record, "Reply123");
        StringAssert.Contains(command, "$HOME/.pi-desktop-artifacts");
        StringAssert.Contains(command, key);
        StringAssert.Contains(command, PosixShell.Quote(record.Name));
        StringAssert.Contains(command, "cat >");
        StringAssert.Contains(command, "test ! -L");
        Assert.AreNotEqual(key, ArtifactTargetStorage.OwnerKey(store.DirectoryPath + "-other"));
        Assert.ThrowsException<IOException>(() => ArtifactTargetStorage.SetupCommand("../outside"));
    }

    [TestMethod]
    [DataRow("wsl")][DataRow("ssh")]
    public async Task RemoteMaterializationTransfersExactBytesAndDeletionDuringTransferCleansCopy(string kind)
    {
        var item = await panel.UploadAsync(await SourceAsync("report.txt", "Binary-safe æøå\r\n"));
        var expected = await File.ReadAllBytesAsync(await store.GetPathAsync(item.Id));
        var target = new ExecutionTarget { Id = Guid.NewGuid(), Name = "Remote", Kind = kind, Host = "test", Path = "/project" };
        var deleteDuringTransfer = false;
        var deletes = 0;
        var remote = new ArtifactTargetStorage(store, target, async (actualTarget, command, bytes, _) =>
        {
            Assert.AreEqual(target, actualTarget);
            if (bytes is null) { deletes++; return ""; }
            CollectionAssert.AreEqual(expected, bytes);
            Assert.IsTrue((await store.RemoteIdsAsync(default)).Contains(item.Id));
            if (deleteDuringTransfer) await store.DeleteAsync(item.Id);
            var marker = System.Text.RegularExpressions.Regex.Match(command, "Artifact[0-9a-f]{32}").Value;
            return "Shell welcome\n" + marker + ":/home/test/.pi-desktop-artifacts/report.txt\n";
        });
        Assert.AreEqual("/home/test/.pi-desktop-artifacts/report.txt", await remote.MaterializeAsync(item.Record, default));
        deleteDuringTransfer = true;
        await Assert.ThrowsExceptionAsync<IOException>(() => remote.MaterializeAsync(item.Record, default));
        Assert.AreEqual(1, deletes);
        Assert.AreEqual(0, (await store.RemoteIdsAsync(default)).Count);
    }

    [TestMethod]
    [DataRow("[]")][DataRow("null")][DataRow("{\"files\":[{\"id\":\"bad\"}]}")]
    public void MalformedAttachmentMetadataRemainsVisibleAndDoesNotCrash(string json)
    {
        var text = "Review\n\n<pi-desktop-attachments>\n" + json + "\n</pi-desktop-attachments>";
        Assert.AreEqual(0, ArtifactPrompt.Read(text).Count);
        Assert.AreEqual(text, ArtifactPrompt.Display(text));
    }
}
