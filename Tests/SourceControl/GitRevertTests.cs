using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class GitRevertTests
{
    [TestMethod]
    public async Task RevertsOnlyWorkingTreeForSelectedLiteralPath()
    {
        var change = new GitChange("file [1].cs", 'M', false, 1, 1);
        var runner = Runner("MM file [1].cs\0");
        await new GitRepositoryService(runner).RevertAsync(State(change), change, default);
        CollectionAssert.AreEqual(new[] { "restore", "--worktree", "--", change.Path }, runner.Commands.Last());
        Assert.IsFalse(runner.Commands.Any(args => args.Contains("--staged")));
    }

    [TestMethod]
    public async Task RejectsStagedConflictedAndStaleFiles()
    {
        foreach (var change in new[] { new GitChange("file", 'M', true, 1, 1), new GitChange("file", 'U', false, 1, 1), new GitChange("file", 'M', false, 1, 1) })
        {
            var runner = Runner("");
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => new GitRepositoryService(runner).RevertAsync(State(change), change, default));
            Assert.IsFalse(runner.Commands.Any(args => args[0] == "restore"));
        }
    }

    [TestMethod]
    public async Task UntrackedFilesUseRecycleBinInsteadOfGitClean()
    {
        var root = Path.Combine(Path.GetTempPath(), "pi-revert-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "new.txt");
            await File.WriteAllTextAsync(path, "keep recoverable");
            var change = new GitChange("new.txt", '?', false, 1, 0);
            var runner = Runner("?? new.txt\0");
            string? recycled = null;
            await new GitRepositoryService(runner, (target, folder) => { Assert.IsFalse(folder); recycled = target; })
                .RevertAsync(State(change) with { Root = root }, change, default);
            Assert.AreEqual(path, recycled);
            Assert.IsFalse(runner.Commands.Any(args => args[0] is "clean" or "restore"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static GitSnapshot State(GitChange change) => new("C:/repo", "refs/heads/main", true, [change], [], [], false);
    private static FakeGitCommandRunner Runner(string status) => new()
    {
        Respond = args => new(0, args[0] == "symbolic-ref" ? "refs/heads/main" : args[0] == "status" ? status : "", "")
    };
}
