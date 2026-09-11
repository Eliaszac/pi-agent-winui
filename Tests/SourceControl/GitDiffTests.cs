using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class GitDiffTests
{
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task LoadsFullContextFromCorrectGitLayerWithoutMutating(bool staged)
    {
        var change = new GitChange("folder/file with spaces.cs", 'M', staged, 1, 1);
        var state = new GitSnapshot("C:/repo", "refs/heads/main", true, [change], [], [], false);
        var runner = new FakeGitCommandRunner
        {
            Respond = args => new(0, args[0] == "symbolic-ref" ? "refs/heads/main" : "@@ -1 +1 @@\n-old\n+new\n", "")
        };
        var content = await new GitRepositoryService(runner).ReadDiffAsync(state, change, default);
        var command = runner.Commands.Single(args => args[0] == "diff");
        Assert.AreEqual(staged, command.Contains("--cached"));
        CollectionAssert.Contains(command, "--unified=2147483647");
        CollectionAssert.Contains(command, "--no-ext-diff");
        CollectionAssert.Contains(command, "--no-textconv");
        Assert.AreEqual("--", command[^2]); Assert.AreEqual(change.Path, command[^1]);
        Assert.AreEqual(staged ? "HEAD" : "Index", content.LeftLabel);
        Assert.AreEqual(staged ? "Index" : "Working tree", content.RightLabel);
        Assert.IsTrue(runner.Commands.All(args => args[0] is "symbolic-ref" or "diff"));
    }

    [TestMethod]
    public async Task BinaryAndConflictedFilesGetExplicitNotices()
    {
        foreach (var change in new[] { new GitChange("file", 'M', false, null, null, true), new GitChange("file", 'U', false, null, null) })
        {
            var state = new GitSnapshot("C:/repo", "refs/heads/main", true, [change], [], [], change.Status == 'U');
            var runner = new FakeGitCommandRunner();
            var content = await new GitRepositoryService(runner).ReadDiffAsync(state, change, default);
            Assert.AreEqual("", content.Patch); Assert.IsTrue(content.Notice.Length > 0);
            Assert.IsFalse(runner.Commands.Any(args => args[0] == "diff"));
        }
    }

    [TestMethod]
    public async Task UntrackedTextUsesEmptyBeforeStateAndRejectsOutsidePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "pi-full-diff-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), "one\ntwo\n");
            var patch = await UntrackedFileDiff.ReadAsync(root, "file.txt", default);
            var document = Utilities.UnifiedDiffParser.Parse(patch);
            Assert.AreEqual(2, document.Lines.Count); Assert.IsTrue(document.Lines.All(line => line.IsAdded));
            await Assert.ThrowsExceptionAsync<IOException>(() => UntrackedFileDiff.ReadAsync(root, "../outside.txt", default));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task RejectsFilesNotInRequestedSnapshot()
    {
        var state = new GitSnapshot("C:/repo", "refs/heads/main", true, [], [], [], false);
        var runner = new FakeGitCommandRunner();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new GitRepositoryService(runner).ReadDiffAsync(state, new("other", 'M', false, 1, 1), default));
        Assert.AreEqual(0, runner.Commands.Count);
    }
}
