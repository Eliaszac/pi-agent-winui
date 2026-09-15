using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.GitHub;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.GitHub;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.Tests.Pi;

namespace PiAgentGui.Tests.GitHub;

[TestClass]
public sealed class GitHubReferenceTests
{
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value) };
    private static GitHubReference Reference => new("owner/repo", 12, false, "Fix", "open");
    [TestMethod]
    public void LocalRepositoryLookupRedirectsEveryStreamUsedByTheRunner()
    {
        var start = ProjectGitHubRepository.CreateStartInfo(new() { Id = Guid.NewGuid(), Name = "Local", Path = @"C:\Projects\Example" });
        Assert.IsTrue(start.RedirectStandardInput);
        Assert.IsTrue(start.RedirectStandardOutput);
        Assert.IsTrue(start.RedirectStandardError);
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow);
        CollectionAssert.AreEqual(new[] { "remote", "get-url", "origin" }, start.ArgumentList.ToArray());
    }
    [TestMethod]
    public async Task OtherRepositoryLinksAreRejectedBeforeAnyRequest()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => throw new AssertFailedException("Must not read another repository.")));
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitHubReferenceReader(new(http)).SearchAsync(
            "https://github.com/other/project/issues/1", false, "owner/repo", "token", default));
    }
    [DataTestMethod]
    [DataRow("https://github.com.evil.test/a/b/issues/1")]
    [DataRow("https://github.com/a/b/issues/0")]
    [DataRow("https://evil@github.com/a/b/issues/1")]
    [DataRow("https://github.com/a/b/issues/1/extra")]
    public void RejectsUntrustedUrls(string url) => Assert.IsNull(GitHubReference.FromUrl(url));

    [TestMethod]
    public void HiddenContextCoexistsWithArtifactsAndSurvivesTranscriptReload()
    {
        var file = new ArtifactRecord(Guid.NewGuid(), "notes.txt", "text/plain", 2, default, default, Guid.NewGuid(), "upload");
        var prompt = ArtifactPrompt.Append(GitHubReferencePrompt.Append("Review this", [Reference with { Content = "Private issue body" }]), [file]);
        var view = new ChatEntryViewModel(new("1", "You", prompt, IsUser: true));
        Assert.AreEqual("Review this", view.Text);
        Assert.AreEqual(Reference.Url, view.GitHubReferences.Single().Url);
        Assert.AreEqual(file.Id, ArtifactPrompt.Read(prompt).Single());
        StringAssert.Contains(prompt, "Private issue body");
    }

    [TestMethod]
    public async Task SearchIncludesClosedItemsAndNeverRequestsWriteAccess()
    {
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            StringAssert.Contains(Uri.UnescapeDataString(request.RequestUri!.Query), "repo:owner/repo");
            Assert.IsFalse(request.RequestUri!.Query.Contains("state%3Aopen"));
            return Json("{\"items\":[{\"number\":12,\"title\":\"Fixed\",\"state\":\"closed\",\"html_url\":\"https://github.com/owner/repo/issues/12\"}]}");
        }));
        var results = await new GitHubReferenceReader(new(http)).SearchAsync("bug", false, "owner/repo", "token", default);
        Assert.AreEqual("closed", results.Single().State);
    }

    [TestMethod]
    public async Task FetchFailureKeepsDraftAndSuccessfulSendHidesContext()
    {
        var fail = true;
        using var http = new HttpClient(new FakeGitHubHandler(request => fail ? new(HttpStatusCode.Forbidden) :
            request.RequestUri!.AbsolutePath.EndsWith("comments") ? Json("[]") : Json("{\"number\":12,\"title\":\"Fix\",\"state\":\"open\",\"body\":\"Issue context\"}")));
        var api = new GitHubApi(http);
        var github = new GitHubViewModel(new(new("id", "slug"), api, new FakeGitHubCredentials { Token = new("token", DateTimeOffset.UtcNow.AddHours(1)) }), api, new FakeGitBranchReader());
        github.Initialize();
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher) { GitHub = github, ReadGitHubRepository = _ => Task.FromResult("owner/repo") };
        await vm.InitializeAsync(); dispatcher.Drain();
        vm.Draft = "Please review"; vm.AttachGitHub(Reference);
        await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.AreEqual("Please review", vm.Draft); Assert.IsTrue(vm.HasPendingGitHub); Assert.AreEqual(0, session.Sent.Count);
        fail = false;
        await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        StringAssert.Contains(session.Sent.Single(), "Issue context");
        Assert.AreEqual("Please review", GitHubReferencePrompt.Display(session.Sent.Single()));
        Assert.IsFalse(vm.HasPendingGitHub);
        session.Emit(new() { IsRunning = true }); dispatcher.Drain();
        vm.Draft = "Follow up"; vm.AttachGitHub(Reference);
        await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.IsTrue(vm.HasQueuedPrompt); Assert.IsFalse(vm.HasPendingGitHub);
        await vm.EditQueuedCommand.ExecuteAsync();
        Assert.AreEqual("Follow up", vm.Draft);
        Assert.AreEqual(Reference.Url, vm.PendingGitHub.Single().Url);
        vm.Draft = "/steer Focus on this";
        await vm.SendCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.AreEqual("Focus on this", GitHubReferencePrompt.Display(session.Steered.Single()));
        Assert.IsFalse(vm.HasPendingGitHub);
    }

    [TestMethod]
    public async Task PullRequestSnapshotIncludesReviewsAndPatchesAndReportsMergedState()
    {
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/files")) return Json("[{\"filename\":\"test.cs\",\"patch\":\"+updated\"}]");
            if (path.EndsWith("/reviews")) return Json("[{\"body\":\"Review body\",\"user\":{\"login\":\"reviewer\"}}]");
            if (path.EndsWith("/comments")) return Json("[{\"body\":\"Comment body\",\"user\":{\"login\":\"author\"}}]");
            if (path.Contains("/pulls/")) return Json("{\"state\":\"closed\",\"merged\":true,\"head\":{\"sha\":\"abc\"},\"base\":{\"sha\":\"def\"}}");
            return Json("{\"number\":12,\"title\":\"Fix\",\"state\":\"closed\",\"pull_request\":{},\"body\":\"Description\"}");
        }));
        var snapshot = await new GitHubReferenceReader(new(http)).FetchAsync(Reference with { IsPullRequest = true }, "token", default);
        Assert.AreEqual("Merged", snapshot.State);
        StringAssert.Contains(snapshot.Content, "Review body"); StringAssert.Contains(snapshot.Content, "+updated");
        StringAssert.Contains(snapshot.Content, "Comment body"); StringAssert.Contains(snapshot.Content, "additional content may be omitted");
    }
}
