using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;
using PiAgentGui.ViewModels.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class SourceControlViewModelTests
{
    private static GitResult Respond(IReadOnlyList<string> args) => new(0, args[0] switch
    {
        "symbolic-ref" => "refs/heads/main\n",
        "rev-parse" => args[1] switch { "--is-inside-work-tree" => "true\n", "--show-toplevel" => "C:/repo\n", _ => "abc123\n" },
        "status" => "MM file.cs\0",
        "diff" => args.Contains("--cached") ? "3\t1\tfile.cs\0" : "1\t0\tfile.cs\0",
        "remote" => "origin\n",
        "for-each-ref" => "refs/heads/main\0\0origin\0refs/heads/main\n",
        _ => ""
    }, "");

    [TestMethod]
    public async Task RefreshKeepsCommitDraftAndSeparatesStagedRows()
    {
        var runner = new FakeGitCommandRunner { Respond = Respond };
        using var model = new SourceControlViewModel(new GitRepositoryService(runner));
        model.SelectProject("C:/repo"); await model.RefreshAsync();
        model.CommitMessage = "draft";
        Assert.IsTrue(model.CanCommit); Assert.IsTrue(model.CanPush);
        Assert.AreEqual(1, model.Staged.Count); Assert.AreEqual(1, model.Changes.Count);
        var staged = model.Staged[0];
        await model.RefreshAsync();
        Assert.AreSame(staged, model.Staged[0]); Assert.AreEqual("draft", model.CommitMessage);
        model.SelectProject("C:/other"); model.CommitMessage = "other draft";
        model.SelectProject("C:/repo"); Assert.AreEqual("draft", model.CommitMessage);
    }

    [TestMethod]
    public async Task FailedCommitPreservesDraftAndSuccessfulCommitClearsIt()
    {
        var fail = true;
        var runner = new FakeGitCommandRunner { Respond = args => args[0] == "commit" && fail ? new(1, "", "hook rejected commit") : Respond(args) };
        using var model = new SourceControlViewModel(new GitRepositoryService(runner));
        model.SelectProject("C:/repo"); await model.RefreshAsync(); model.CommitMessage = "keep me";
        await model.CommitAsync();
        Assert.IsTrue(model.HasError); Assert.AreEqual("keep me", model.CommitMessage); Assert.IsFalse(model.IsBusy);
        fail = false; await model.CommitAsync();
        Assert.IsFalse(model.HasError); Assert.AreEqual("", model.CommitMessage); Assert.IsTrue(model.HasNotice);
    }

    [TestMethod]
    public async Task ReportsRepositoryErrorsInsteadOfPretendingRepositoryIsMissing()
    {
        var runner = new FakeGitCommandRunner { Respond = _ => new(128, "", "fatal: detected dubious ownership") };
        using var model = new SourceControlViewModel(new GitRepositoryService(runner));
        model.SelectProject("C:/repo"); await model.RefreshAsync();
        Assert.IsTrue(model.HasError); Assert.IsFalse(model.CanAct);
        runner.Respond = Respond; await model.RefreshAsync();
        Assert.IsFalse(model.HasError); Assert.IsTrue(model.HasRepository);
    }
}
