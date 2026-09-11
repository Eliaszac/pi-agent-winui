using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class InstructionFileTests
{
    private string root = "";
    [TestInitialize] public void Setup() => root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests", Guid.NewGuid().ToString("N"))).FullName;
    [TestCleanup] public void Cleanup()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe cleanup path");
        Directory.Delete(root, true);
    }

    [TestMethod]
    [DataRow(false, "\n")]
    [DataRow(true, "\r\n")]
    public async Task SavePreservesUtf8BomAndLineEndingsAndFlagsStaleContext(bool bom, string newline)
    {
        var path = Path.Combine(root, "AGENTS.md");
        await File.WriteAllTextAsync(path, "Original" + newline, new UTF8Encoding(bom));
        var store = new InstructionFileStore();
        var original = await store.ReadAsync(path);
        var panel = new InstructionsPanelViewModel(store);
        panel.Select(new(true, [new(path, original.ContentHash)]), true, root);
        await panel.RefreshAsync();
        Assert.AreEqual("Loaded · matches disk", panel.Items.Single().Status);
        await store.SaveAsync(original, "Changed\rNext\r");
        var saved = await store.ReadAsync(path);
        Assert.AreEqual(bom, saved.HasBom);
        Assert.AreEqual("Changed" + newline + "Next" + newline, saved.Text);
        await panel.RefreshAsync();
        Assert.AreEqual("Disk differs · reload required", panel.Items.Single().Status);
        panel.Select(new(true, [new(path, saved.ContentHash)]), true, root);
        await panel.RefreshAsync();
        Assert.AreEqual("Loaded · matches disk", panel.Items.Single().Status);
        Assert.AreEqual(1, Directory.GetFiles(root).Length);
    }

    [TestMethod]
    public async Task OutsideEditsAreNotOverwritten()
    {
        var path = Path.Combine(root, "CLAUDE.md");
        await File.WriteAllTextAsync(path, "Original");
        var store = new InstructionFileStore();
        var original = await store.ReadAsync(path);
        await File.WriteAllTextAsync(path, "Outside change");
        await Assert.ThrowsExceptionAsync<IOException>(() => store.SaveAsync(original, "My draft"));
        Assert.AreEqual("Outside change", await File.ReadAllTextAsync(path));
        Assert.AreEqual(1, Directory.GetFiles(root).Length);
    }

    [TestMethod]
    public async Task ConcurrentEditorsCannotSilentlyOverwriteEachOther()
    {
        var path = Path.Combine(root, "AGENTS.override.md");
        await File.WriteAllTextAsync(path, "Original");
        var first = new InstructionFileStore(); var second = new InstructionFileStore();
        var original = await first.ReadAsync(path);
        await first.SaveAsync(original, "First save");
        await Assert.ThrowsExceptionAsync<IOException>(() => second.SaveAsync(original, "Second save"));
        Assert.AreEqual("First save", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public void SnapshotContainsOnlyValidatedIdentitiesAndHashes()
    {
        var path = Path.Combine(root, "AGENTS.md");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("content")));
        var json = System.Text.Json.JsonSerializer.Serialize(new { version = 1, available = true, files = new[] { new { path, hash } } });
        Assert.AreEqual(path, InstructionSnapshotParser.Parse(json)!.Files.Single().Path);
        Assert.IsNull(InstructionSnapshotParser.Parse("{\"version\":1,\"files\":[{\"path\":\"relative.md\",\"hash\":\"bad\"}]}"));
        Assert.IsNull(InstructionSnapshotParser.Parse("not JSON"));
    }

    [TestMethod]
    public async Task UnsupportedEncodingAndArbitraryFilesAreRejected()
    {
        var path = Path.Combine(root, "AGENTS.md");
        await File.WriteAllTextAsync(path, "UTF16", Encoding.Unicode);
        await Assert.ThrowsExceptionAsync<IOException>(() => new InstructionFileStore().ReadAsync(path));
        await Assert.ThrowsExceptionAsync<IOException>(() => new InstructionFileStore().ReadAsync(Path.Combine(root, "other.md")));
    }
}
