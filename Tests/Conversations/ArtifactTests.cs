using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Conversations;

[TestClass]
public sealed class ArtifactTests
{
    private string root = null!;
    private ArtifactStore store = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "pi-artifacts-test-" + Guid.NewGuid().ToString("N"));
        store = new(Path.Combine(root, "session.jsonl.artifacts"));
    }
    [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private ExecutionTarget Target(string kind = "local") => new() { Id = Guid.NewGuid(), Name = "Test", Path = kind == "local" ? root : "/workspace", Kind = kind, Host = kind == "wsl" ? "Ubuntu" : "test-server" };

    [TestMethod]
    public async Task UpdatesKeepIdentityAndRemoveOldVersionsAndRenamePersists()
    {
        var first = await store.SaveAsync("report.md", Encoding.UTF8.GetBytes("first"), "text/plain", "Agent");
        var oldPath = await store.GetPathAsync(first.Id);
        var next = await store.SaveAsync("report.md", Encoding.UTF8.GetBytes("second"), "text/plain", "Agent");
        Assert.AreEqual(first.Id, next.Id);
        Assert.AreEqual(first.CreatedAt, next.CreatedAt);
        Assert.IsFalse(File.Exists(oldPath));
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(oldPath)));
        var renamed = await store.RenameAsync(first.Id, "notes.txt");
        var reloaded = new ArtifactStore(store.DirectoryPath);
        Assert.AreEqual("notes.txt", (await reloaded.ListAsync()).Single().Name);
        Assert.AreEqual("second", await File.ReadAllTextAsync(await reloaded.GetPathAsync(first.Id)));
        Assert.AreEqual(1, Directory.GetFiles(store.DirectoryPath, "*", SearchOption.AllDirectories).Count(path => !path.EndsWith(".json")));
    }

    [TestMethod]
    public async Task DeletionKeepsTombstoneAndScreenshotReloadDoesNotResurrectIt()
    {
        var panel = new ArtifactPanelViewModel(store, Target());
        var image = new ChatEntry("tool:screen", "browser", "", Images: [new(Convert.ToBase64String([1, 2, 3]))]);
        await panel.ObserveImagesAsync([image]);
        var item = panel.Items.Single();
        var path = await store.GetPathAsync(item.Id);
        await store.DeleteAsync(item.Id);
        await panel.RefreshAsync();
        Assert.AreEqual(0, panel.Items.Count);
        Assert.IsFalse(item.Available);
        Assert.AreEqual("Deleted", item.Description);
        Assert.IsFalse(File.Exists(path));
        var reopened = new ArtifactPanelViewModel(new(store.DirectoryPath), Target());
        await reopened.ObserveImagesAsync([image]);
        Assert.AreEqual(0, reopened.Items.Count);
        Assert.IsTrue((await store.ListAsync()).Single().Deleted);
    }

    [TestMethod]
    public async Task CopyRetainsIdsButFilesAndDeletionAreIndependent()
    {
        var first = await store.SaveAsync("hello.txt", [4, 5], "text/plain", "Agent");
        var deleted = await store.SaveAsync("removed.txt", [1], "text/plain", "Agent");
        await store.DeleteAsync(deleted.Id);
        var copy = new ArtifactStore(Path.Combine(root, "copy.jsonl.artifacts"));
        await store.CopyToAsync(copy);
        await store.DeleteAllAsync();
        Assert.IsFalse(Directory.Exists(store.DirectoryPath));
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(await copy.GetPathAsync(first.Id)));
        Assert.IsTrue((await copy.ListAsync()).Single(item => item.Id == deleted.Id).Deleted);
    }

    [TestMethod]
    public async Task ConcurrentSavesProduceOneCompleteCurrentFile()
    {
        await Task.WhenAll(Enumerable.Range(0, 16).Select(index => new ArtifactStore(store.DirectoryPath).SaveAsync("same.txt", Encoding.UTF8.GetBytes(index.ToString()), "text/plain", "Agent")));
        var record = (await store.ListAsync()).Single();
        Assert.IsTrue(int.TryParse(await File.ReadAllTextAsync(await store.GetPathAsync(record.Id)), out _));
        Assert.AreEqual(2, Directory.GetFiles(store.DirectoryPath, "*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    [DataRow("../escape.txt")][DataRow("a\\b.txt")][DataRow("CON.txt")][DataRow("NUL")][DataRow("trailing.")][DataRow("bad:name")][DataRow(" ")]
    public void InvalidNamesAreRejected(string name) => Assert.ThrowsException<IOException>(() => ArtifactStore.ValidateName(name));

    [TestMethod]
    public async Task ForeignIdsAndDuplicateRenameCannotReplaceAnotherArtifact()
    {
        var a = await store.SaveAsync("a.txt", [1], "text/plain", "Agent");
        var b = await store.SaveAsync("b.txt", [2], "text/plain", "Agent");
        await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveAsync("a.txt", [9], "text/plain", "Agent", Guid.NewGuid()));
        await Assert.ThrowsExceptionAsync<IOException>(() => store.RenameAsync(a.Id, "b.txt"));
        await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveAsync("b.txt", [9], "text/plain", "Agent", a.Id));
        CollectionAssert.AreEqual(new byte[] { 2 }, await File.ReadAllBytesAsync(await store.GetPathAsync(b.Id)));
    }

    [TestMethod]
    public async Task LocalImportCopiesBinaryWithoutChangingSourceAndRejectsOversize()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "source.bin");
        byte[] bytes = [0, 255, 128, 10, 13, 17];
        await File.WriteAllBytesAsync(path, bytes);
        var panel = new ArtifactPanelViewModel(store, Target());
        using var request = JsonDocument.Parse(JsonSerializer.Serialize(new { action = "save", name = "binary.bin", sourcePath = path }));
        var saved = await panel.HandleAsync(request.RootElement);
        Assert.AreEqual(1, panel.Items.Count);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(await store.GetPathAsync(Guid.Parse(saved["artifactId"]!.GetValue<string>()))));
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
        await using var oversized = new MemoryStream(new byte[ArtifactStore.MaximumBytes + 1], false);
        await Assert.ThrowsExceptionAsync<IOException>(() => ArtifactSourceReader.ReadBoundedAsync(oversized, default));
    }

    [TestMethod]
    [DataRow("wsl")][DataRow("ssh")]
    public async Task RemoteConversationsCanCreateTextWithoutConnectingAndUseTargetForImports(string kind)
    {
        var panel = new ArtifactPanelViewModel(store, Target(kind));
        using var request = JsonDocument.Parse("""{"action":"save","name":"report.md","content":"Hello æøå"}""");
        await panel.HandleAsync(request.RootElement);
        var item = panel.Items.Single();
        Assert.AreEqual("Hello æøå", await File.ReadAllTextAsync(await store.GetPathAsync(item.Id)));
        StringAssert.Contains(item.Description, kind.ToUpperInvariant());
        const string path = "/tmp/a ' $(touch ignored).pdf";
        var command = ArtifactSourceReader.ImportCommand(path);
        StringAssert.Contains(command, PosixShell.Quote(path));
        var start = TargetCommandRunner.CreateStartInfo(Target(kind), command);
        Assert.IsTrue(start.CreateNoWindow);
        Assert.IsTrue(start.RedirectStandardOutput);
        CollectionAssert.Contains(start.ArgumentList.ToArray(), Target(kind).Host);
        Assert.ThrowsException<IOException>(() => ArtifactSourceReader.ImportCommand("relative.pdf"));
    }

    [TestMethod]
    public void PersistedAndLiveSuccessfulResultsHaveStandaloneCards()
    {
        var id = Guid.NewGuid();
        var transcript = new PiTranscript();
        using var live = JsonDocument.Parse(JsonSerializer.Serialize(new { type = "tool_execution_end", toolCallId = "a", toolName = "artifact_save", result = new { content = Array.Empty<object>(), details = new { artifactId = id } } }));
        var entry = transcript.Apply(live.RootElement)!;
        Assert.AreEqual(id, entry.ArtifactId);
        using var history = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new { role = "toolResult", toolCallId = "a", toolName = "artifact_save", content = Array.Empty<object>(), details = new { artifactId = id } } }));
        Assert.AreEqual(id, transcript.Load(history.RootElement).Single().ArtifactId);
        using var details = JsonDocument.Parse(JsonSerializer.Serialize(new { artifactId = id }));
        Assert.IsNull(ArtifactResult.Read("bash", true, details.RootElement));
        Assert.IsNull(ArtifactResult.Read("artifact_save", false, details.RootElement));
        var card = new ChatEntryViewModel(entry) { Artifact = new ArtifactPanelViewModel(store, Target()).Get(id) };
        var rows = new TranscriptPresentation().Project([new(new("tool:before", "read", "", IsTool: true)), card, new(new("tool:after", "bash", "", IsTool: true))]);
        Assert.AreEqual(3, rows.Count);
        Assert.AreSame(card, rows[1]);
        Assert.IsTrue(card.IsArtifact);
        Assert.IsFalse(card.IsMessage || card.IsTool || card.IsLeftAligned);
    }

    [TestMethod]
    public async Task CancelledOwnerCannotSaveMoreData()
    {
        var panel = new ArtifactPanelViewModel(store, Target());
        panel.Cancel();
        using var request = JsonDocument.Parse("""{"action":"save","name":"report.md","content":"Hello"}""");
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => panel.HandleAsync(request.RootElement));
        Assert.AreEqual(0, (await store.ListAsync()).Count);
    }

    [TestMethod]
    public void RemoteFramingPreservesBinaryBytesAndIgnoresShellBanners()
    {
        var frame = new ArtifactTransferFrame();
        byte[] bytes = [0, 255, 13, 10, 127, 128];
        byte[] response = [.. Encoding.UTF8.GetBytes("Welcome to the target" + frame.Header), .. bytes, .. Encoding.UTF8.GetBytes(frame.Footer + "Goodbye")];
        CollectionAssert.AreEqual(bytes, frame.Decode(response));
        Assert.ThrowsException<IOException>(() => frame.Decode(Encoding.UTF8.GetBytes(frame.Header + "truncated")));
        Assert.ThrowsException<IOException>(() => new ArtifactTransferFrame().Decode(response));
    }

    [TestMethod]
    public async Task EditingScreenshotArtifactDoesNotLoseItsImportIdentity()
    {
        var original = await store.SaveAsync("image.png", [1], "image/png", "Screenshot", sourceKey: "screenshot:tool:1");
        await store.SaveAsync("edited.png", [2], "image/png", "Agent", original.Id, "tool:update");
        var reloaded = await store.SaveAsync("image.png", [1], "image/png", "Screenshot", sourceKey: "screenshot:tool:1");
        Assert.AreEqual(original.Id, reloaded.Id);
        Assert.AreEqual("edited.png", reloaded.Name);
        CollectionAssert.AreEqual(new byte[] { 2 }, await File.ReadAllBytesAsync(await store.GetPathAsync(original.Id)));
    }
}
