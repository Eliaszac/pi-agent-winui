using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class GitConflictServiceTests
{
    private string root = null!;
    private const string FileName = "file [1].txt";
    private static readonly string Oid = new('a', 40);
    private static string Entries => $"100644 {Oid} 1\t{FileName}\0" + $"100644 {Oid} 2\t{FileName}\0" + $"100644 {Oid} 3\t{FileName}\0";
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "pi-conflict-" + Guid.NewGuid()); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);
    private GitSnapshot State => new(root, "refs/heads/main", true, [Change], [], [], true);
    private static GitChange Change => new(FileName, 'U', false, null, null);
    private static FakeGitCommandRunner Runner(string? entries = null) => new()
    {
        Respond = args => new(0, args[0] switch { "rev-parse" => "commit", "ls-files" => entries ?? Entries, "cat-file" => args[1] == "-s" ? "5" : "hello", _ => "" }, "")
    };

    [TestMethod]
    public async Task ReadsStagesAndAppliesOnlySelectedFilePreservingBom()
    {
        var path = Path.Combine(root, FileName);
        await File.WriteAllTextAsync(path, "working\r\n", new UTF8Encoding(true));
        var runner = Runner(); var service = new GitConflictService(runner);
        var conflict = await service.ReadAsync(State, Change, default);
        Assert.IsTrue(conflict.HasBom); Assert.AreEqual("hello", conflict.Base); Assert.AreEqual("working\r\n", conflict.WorkingText);
        await service.ApplyAsync(conflict, "resolved\r\n", false, default);
        var bytes = await File.ReadAllBytesAsync(path);
        CollectionAssert.AreEqual(new byte[] { 239, 187, 191 }, bytes[..3]);
        Assert.AreEqual("resolved\r\n", Encoding.UTF8.GetString(bytes[3..]));
        CollectionAssert.AreEqual(new[] { "add", "--all", "--", FileName }, runner.Commands.Last());
        Assert.IsFalse(runner.Commands.Any(command => command[0] is "commit" or "merge" or "rebase"));
    }

    [TestMethod]
    public async Task RefusesExternalEditsAndIndexChanges()
    {
        var path = Path.Combine(root, FileName);
        await File.WriteAllTextAsync(path, "original");
        var runner = Runner(); var service = new GitConflictService(runner);
        var conflict = await service.ReadAsync(State, Change, default);
        await File.WriteAllTextAsync(path, "external edit");
        await Assert.ThrowsExceptionAsync<IOException>(() => service.ApplyAsync(conflict, "result", false, default));
        Assert.AreEqual("external edit", await File.ReadAllTextAsync(path));
        await File.WriteAllTextAsync(path, "original");
        var changed = Runner("");
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitConflictService(changed).ApplyAsync(conflict, "result", false, default));
        Assert.AreEqual("original", await File.ReadAllTextAsync(path));
        Assert.IsFalse(runner.Commands.Concat(changed.Commands).Any(command => command[0] == "add"));
    }

    [TestMethod]
    public async Task BlocksRemainingMarkersAndUnsupportedModes()
    {
        var service = new GitConflictService(Runner());
        var conflict = await service.ReadAsync(State, Change, default);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.ApplyAsync(conflict, "<<<<<<< ours\n", false, default));
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitConflictService(Runner(Entries.Replace("100644", "120000"))).ReadAsync(State, Change, default));
    }

    [TestMethod]
    public async Task BinaryAndNonUtf8WorkingFilesAreNotOverwritten()
    {
        var path = Path.Combine(root, FileName);
        await File.WriteAllBytesAsync(path, [0, 1, 2]);
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitConflictService(Runner()).ReadAsync(State, Change, default));
        await File.WriteAllBytesAsync(path, [0xff, 0xff]);
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitConflictService(Runner()).ReadAsync(State, Change, default));
    }
}
