using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class GitRepositoryServiceTests
{
    private static GitSnapshot State(bool hasHead = true) => new("C:\\repo", "refs/heads/main", hasHead, [],
        [new("refs/heads/main", "main", false, "origin", "refs/heads/trunk", true),
         new("refs/heads/topic", "topic", false, null, null, false),
         new("refs/remotes/origin/topic", "origin/topic", true, "origin", "refs/heads/topic", false)], ["origin"], false);

    [TestMethod]
    public async Task UsesLiteralPathsAndUnstagesWithoutChangingWorkingFiles()
    {
        var runner = new FakeGitCommandRunner(); var service = new GitRepositoryService(runner);
        var change = new GitChange(":(glob)* file.cs", 'M', false, 1, 2);
        await service.StageAsync(State(), change, default);
        CollectionAssert.AreEqual(new[] { "add", "--all", "--", change.Path }, runner.Commands.Last());
        await service.UnstageAsync(State(), change, default);
        CollectionAssert.AreEqual(new[] { "restore", "--staged", "--", change.Path }, runner.Commands.Last());
        await service.UnstageAsync(State(false), change, default);
        CollectionAssert.AreEqual(new[] { "rm", "--cached", "-r", "--ignore-unmatch", "--", change.Path }, runner.Commands.Last());
    }

    [TestMethod]
    public async Task CommitsOnlyIndexWithMessageAsOneArgument()
    {
        var runner = new FakeGitCommandRunner(); var service = new GitRepositoryService(runner);
        var message = "first line\n\nliteral `text` and $(text)";
        await service.CommitAsync(State(), message, default);
        CollectionAssert.AreEqual(new[] { "commit", "-m", message }, runner.Commands.Last());
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.CommitAsync(State(), "  ", default));
    }

    [TestMethod]
    public async Task RefusesMutationWhenCurrentBranchChanged()
    {
        var runner = new FakeGitCommandRunner { Head = "refs/heads/different" };
        var service = new GitRepositoryService(runner);
        await Assert.ThrowsExceptionAsync<IOException>(() => service.CommitAsync(State(), "message", default));
        Assert.AreEqual(1, runner.Commands.Count);
        Assert.AreEqual("symbolic-ref", runner.Commands[0][0]);
    }

    [TestMethod]
    public async Task PushUsesExplicitUpstreamAndPullCannotCreateMerge()
    {
        var runner = new FakeGitCommandRunner(); var service = new GitRepositoryService(runner);
        await service.PushAsync(State(), "origin", default);
        CollectionAssert.AreEqual(new[] { "-c", "push.followTags=false", "push", "--set-upstream", "--", "origin", "HEAD:refs/heads/trunk" }, runner.Commands.Last());
        await service.PullAsync(State(), default);
        CollectionAssert.Contains(runner.Commands.Last(), "--ff-only");
        CollectionAssert.Contains(runner.Commands.Last(), "--no-autostash");
    }

    [TestMethod]
    public async Task BranchActionsKeepFetchSeparateFromCheckoutAndMerge()
    {
        var runner = new FakeGitCommandRunner(); var service = new GitRepositoryService(runner); var state = State();
        await service.FetchAsync(state, state.Branches[2], default);
        CollectionAssert.AreEqual(new[] { "fetch", "--", "origin", "refs/heads/topic:refs/remotes/origin/topic" }, runner.Commands.Last());
        await service.CheckoutAsync(state, state.Branches[2], null, default);
        CollectionAssert.AreEqual(new[] { "switch", "--no-guess", "--track", "-c", "topic", "refs/remotes/origin/topic" }, runner.Commands.Last());
        await service.CheckoutAsync(state, state.Branches[1], "new-topic", default);
        CollectionAssert.AreEqual(new[] { "switch", "--no-guess", "--no-track", "-c", "new-topic", "refs/heads/topic" }, runner.Commands.Last());
        await service.MergeAsync(state, state.Branches[1], default);
        CollectionAssert.AreEqual(new[] { "merge", "--no-edit", "--no-autostash", "refs/heads/topic" }, runner.Commands.Last());
    }

    [TestMethod]
    public async Task InvalidBranchNameNeverReachesSwitch()
    {
        var runner = new FakeGitCommandRunner(); var service = new GitRepositoryService(runner);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.CheckoutAsync(State(), State().Branches[1], "--force", default));
        Assert.IsFalse(runner.Commands.Any(command => command[0] == "switch"));
    }
}
