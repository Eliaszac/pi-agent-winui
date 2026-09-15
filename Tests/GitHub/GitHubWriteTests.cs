using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Services.GitHub;

namespace PiAgentGui.Tests.GitHub;

[TestClass]
public sealed class GitHubWriteTests
{
    private static HttpResponseMessage Item(bool pr = false, string body = "Old body", string updated = "one") => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object?>
        { ["title"] = "Old title", ["body"] = body, ["state"] = "open", ["updated_at"] = updated }
        .Concat(pr ? new Dictionary<string, object?> { ["pull_request"] = new { } } : []) .ToDictionary())) };
    private static Task<string> Repo(CancellationToken _) => Task.FromResult("owner/repo");

    [TestMethod]
    public async Task IssueWritesOnlyApprovedFieldsAfterShowingBeforeAndAfter()
    {
        var approved = false; var reads = 0; var writes = 0;
        using var http = new HttpClient(new FakeGitHubHandler(message =>
        {
            Assert.AreEqual("/repos/owner/repo/issues/12", message.RequestUri!.AbsolutePath);
            if (message.Method == HttpMethod.Get) { reads++; return Item(); }
            Assert.IsTrue(approved); Assert.AreEqual(2, reads); Assert.AreEqual(HttpMethod.Patch, message.Method); writes++;
            Assert.AreEqual("{\"body\":\"\",\"state\":\"closed\"}", message.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return Item();
        }));
        var result = await new GitHubWriter(new(http)).ExecuteAsync(new("update_issue", 12, Body: "", State: "closed"), "token", Repo,
            (preview, _) => { StringAssert.Contains(preview, "Old body"); StringAssert.Contains(preview, "closed"); approved = true; return Task.FromResult(true); }, () => { }, default);
        Assert.AreEqual(1, writes); Assert.IsTrue(result["success"]!.GetValue<bool>());
    }

    [TestMethod]
    public async Task BlockingDoesNotWrite()
    {
        using var http = new HttpClient(new FakeGitHubHandler(message => { Assert.AreEqual(HttpMethod.Get, message.Method); return Item(); }));
        var result = await new GitHubWriter(new(http)).ExecuteAsync(new("update_issue", 12, Title: "New"), "token", Repo, (_, _) => Task.FromResult(false), () => { }, default);
        Assert.IsTrue(result["cancelled"]!.GetValue<bool>());
    }

    [TestMethod]
    public async Task ChangedIssueOrRepositoryPreventsWrite()
    {
        foreach (var changedRepository in new[] { false, true })
        {
            var reads = 0; var approved = false;
            using var http = new HttpClient(new FakeGitHubHandler(message =>
            {
                Assert.AreEqual(HttpMethod.Get, message.Method);
                return Item(updated: ++reads == 1 ? "one" : "two");
            }));
            await Assert.ThrowsExceptionAsync<IOException>(() => new GitHubWriter(new(http)).ExecuteAsync(new("update_issue", 12, Title: "New"), "token",
                _ => Task.FromResult(changedRepository && approved ? "other/repo" : "owner/repo"),
                (_, _) => { approved = true; return Task.FromResult(true); }, () => { }, default));
        }
    }

    [TestMethod]
    public async Task CommentUsesIssueCommentsEndpointAndReturnsPrLink()
    {
        var writes = 0;
        using var http = new HttpClient(new FakeGitHubHandler(message =>
        {
            if (message.Method == HttpMethod.Get) return Item(pr: true);
            writes++; Assert.AreEqual(HttpMethod.Post, message.Method);
            Assert.AreEqual("/repos/owner/repo/issues/12/comments", message.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.Created) { Content = new StringContent("{\"id\":123}") };
        }));
        var result = await new GitHubWriter(new(http)).ExecuteAsync(new("comment_pr", 12, Body: "Looks good"), "token", Repo,
            (preview, _) => { StringAssert.Contains(preview, "Looks good"); return Task.FromResult(true); }, () => { }, default);
        Assert.AreEqual(1, writes); Assert.AreEqual("https://github.com/owner/repo/pull/12#issuecomment-123", result["url"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task WrongItemTypeIsRejectedBeforeApproval()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => Item(pr: true)));
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitHubWriter(new(http)).ExecuteAsync(new("update_issue", 12, State: "closed"), "token", Repo,
            (_, _) => throw new AssertFailedException("Must reject before approval"), () => { }, default));
    }

    [TestMethod]
    public async Task UnknownOutcomeIsNotRetried()
    {
        var writes = 0;
        using var http = new HttpClient(new FakeGitHubHandler(message =>
        {
            if (message.Method == HttpMethod.Get) return Item(pr: true);
            writes++; throw new HttpRequestException("Connection lost");
        }));
        var error = await Assert.ThrowsExceptionAsync<IOException>(() => new GitHubWriter(new(http)).ExecuteAsync(new("comment_pr", 12, Body: "Comment"), "token", Repo,
            (_, _) => Task.FromResult(true), () => { }, default));
        Assert.AreEqual(1, writes); StringAssert.Contains(error.Message, "outcome is unknown");
    }

    [TestMethod]
    public async Task InvalidFieldsAndRepositoryNeverReachGitHub()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => throw new AssertFailedException("Unexpected HTTP request")));
        var writer = new GitHubWriter(new(http));
        foreach (var request in new GitHubWriteRequest[] { new("merge", 12), new("update_issue", 0, Title: "New"),
            new("update_issue", 12), new("update_issue", 12, State: "merged"), new("comment_pr", 12, Body: " "),
            new("comment_pr", 12, Title: "Unexpected", Body: "Comment"), new("update_issue", 12, Body: new string('x', 60001)) })
            await Assert.ThrowsExceptionAsync<IOException>(() => writer.ExecuteAsync(request, "token", Repo, (_, _) => Task.FromResult(true), () => { }, default));
        await Assert.ThrowsExceptionAsync<IOException>(() => writer.ExecuteAsync(new("update_issue", 12, Title: "New"), "token",
            _ => Task.FromResult("owner/repo/../../other"), (_, _) => Task.FromResult(true), () => { }, default));
    }

    [TestMethod]
    public async Task PermissionFailureIsActionableAndNotRetried()
    {
        var writes = 0;
        using var http = new HttpClient(new FakeGitHubHandler(message =>
        {
            if (message.Method == HttpMethod.Get) return Item();
            writes++; return new(HttpStatusCode.Forbidden);
        }));
        var error = await Assert.ThrowsExceptionAsync<IOException>(() => new GitHubWriter(new(http)).ExecuteAsync(new("update_issue", 12, Title: "New"), "token", Repo,
            (_, _) => Task.FromResult(true), () => { }, default));
        Assert.AreEqual(1, writes); StringAssert.Contains(error.Message, "Read and write");
    }

    [TestMethod]
    public async Task CancellationOrDisconnectAfterApprovalPreventsWrite()
    {
        foreach (var cancel in new[] { false, true })
        {
            using var lifetime = new CancellationTokenSource();
            var approved = false;
            using var http = new HttpClient(new FakeGitHubHandler(message => { Assert.AreEqual(HttpMethod.Get, message.Method); return Item(); }));
            try
            {
                await new GitHubWriter(new(http)).ExecuteAsync(new("update_issue", 12, Title: "New"), "token", Repo,
                    (_, _) => { approved = true; if (cancel) lifetime.Cancel(); return Task.FromResult(true); },
                    () => { if (approved && !cancel) throw new IOException("Disconnected"); }, lifetime.Token);
                Assert.Fail("The write must be rejected");
            }
            catch (Exception error) when (error is IOException or OperationCanceledException) { }
        }
    }
}
