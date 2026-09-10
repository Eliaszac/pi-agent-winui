using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Services.GitHub;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.GitHub;

namespace PiAgentGui.Tests.GitHub;

[TestClass]
public sealed class GitHubTests
{
    [TestMethod]
    public async Task ConnectButtonRequiresRepositoryEvenWhenSignedOut()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => throw new AssertFailedException("Signed-out checks must stay local.")));
        var api = new GitHubApi(http);
        var reader = new FakeGitBranchReader { IsRepository = false, Branch = null };
        var vm = new GitHubViewModel(new(new("id", "slug"), api, new FakeGitHubCredentials()), api, reader);
        vm.Select("folder");
        Assert.IsFalse(vm.ShowHeaderButton);
        await vm.RefreshAsync(["folder"], default);
        Assert.IsFalse(vm.ShowHeaderButton);
        // An initialized repository need not have a commit, remote or GitHub branch yet.
        reader.IsRepository = true;
        await vm.RefreshAsync(["folder"], default);
        Assert.IsTrue(vm.ShowHeaderButton);
        vm.Select("another-folder");
        Assert.IsFalse(vm.ShowHeaderButton);
        reader.IsRepository = false;
        await vm.RefreshAsync(["another-folder"], default);
        Assert.IsFalse(vm.ShowHeaderButton);
    }
    [DataTestMethod]
    [DataRow("git@github.com:owner/repo.git")]
    [DataRow("https://github.com/owner/repo.git")]
    [DataRow("ssh://git@github.com/owner/repo.git")]
    public void ParsesSupportedRemoteForms(string remote) => Assert.AreEqual(new GitHubBranch("owner", "repo", "feature/a"), GitHubRemote.Parse(remote, "feature/a"));

    [DataTestMethod]
    [DataRow("https://github.com.evil.example/owner/repo")]
    [DataRow("http://github.com/owner/repo")]
    [DataRow("https://github.com/owner/repo/extra")]
    [DataRow("git@gitlab.com:owner/repo.git")]
    [DataRow("C:/local/repo")]
    public void RejectsUnsupportedOrMisleadingRemotes(string remote) => Assert.IsNull(GitHubRemote.Parse(remote, "feature"));

    [TestMethod]
    public async Task PullLookupValidatesHeadRepositoryAndBuildsTrustedUrl()
    {
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            Assert.AreEqual("api.github.com", request.RequestUri!.Host);
            Assert.AreEqual("test-token", request.Headers.Authorization!.Parameter);
            if (!request.RequestUri.AbsolutePath.EndsWith("pulls")) return Json("{}");
            StringAssert.Contains(request.RequestUri.Query, "state=open");
            return Json("""
                [{"number":1,"title":"Wrong fork","head":{"ref":"feature","repo":{"full_name":"other/repo"}}},
                 {"number":42,"title":"Correct","html_url":"https://evil.example/","head":{"ref":"feature","repo":{"full_name":"owner/repo"}}}]
                """);
        }));
        var pull = await new GitHubApi(http).FindPullRequestAsync(new("owner", "repo", "feature"), "test-token", default);
        Assert.AreEqual(42, pull!.Number);
        Assert.AreEqual("https://github.com/owner/repo/pull/42", pull.Url.AbsoluteUri);
    }

    [TestMethod]
    public async Task ForkLookupChecksParentRepository()
    {
        using var http = new HttpClient(new FakeGitHubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("pulls")
            ? Json("""[{"number":8,"title":"Fork PR","head":{"ref":"feature","repo":{"full_name":"owner/repo"}}}]""")
            : Json("""{"parent":{"full_name":"upstream/repo"}}""")));
        var pull = await new GitHubApi(http).FindPullRequestAsync(new("owner", "repo", "feature"), "token", default);
        Assert.AreEqual("https://github.com/upstream/repo/pull/8", pull!.Url.AbsoluteUri);
    }

    [TestMethod]
    public async Task BranchSwitchClearsBadgeAndDisconnectRestoresConnectButton()
    {
        using var http = new HttpClient(new FakeGitHubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("pulls")
            ? Json("""[{"number":42,"title":"PR","head":{"ref":"feature","repo":{"full_name":"owner/repo"}}}]""") : Json("{}")));
        var api = new GitHubApi(http);
        var store = new FakeGitHubCredentials { Token = new("token", DateTimeOffset.UtcNow.AddHours(1)) };
        var reader = new FakeGitBranchReader();
        var viewModel = new GitHubViewModel(new(new("id", "slug"), api, store), api, reader);
        viewModel.Initialize();
        viewModel.Select("project");
        await viewModel.RefreshAsync(["project"], default);
        Assert.AreEqual("Open PR", viewModel.HeaderLabel);
        Assert.IsTrue(viewModel.ShowHeaderButton);
        reader.Branch = null;
        await viewModel.RefreshAsync(["project"], default);
        Assert.IsNull(viewModel.SelectedPullRequest);
        Assert.IsFalse(viewModel.ShowHeaderButton);
        viewModel.Disconnect();
        Assert.IsNull(store.Token);
        Assert.AreEqual("Connect to GitHub", viewModel.HeaderLabel);
        Assert.IsTrue(viewModel.ShowHeaderButton);
    }

    [TestMethod]
    public async Task UnauthorizedResponseClearsCredentials()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => new(HttpStatusCode.Unauthorized)));
        var api = new GitHubApi(http);
        var store = new FakeGitHubCredentials { Token = new("token", DateTimeOffset.UtcNow.AddHours(1)) };
        var vm = new GitHubViewModel(new(new("id", "slug"), api, store), api, new FakeGitBranchReader());
        vm.Initialize();
        vm.Select("project");
        await vm.RefreshAsync(["project"], default);
        Assert.IsFalse(vm.IsConnected);
        Assert.IsNull(store.Token);
        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public async Task DeviceSignInRequiresConfigurationAndCancellationDoesNotSave()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => throw new AssertFailedException("No HTTP request expected.")));
        var api = new GitHubApi(http);
        var store = new FakeGitHubCredentials();
        await Assert.ThrowsExceptionAsync<IOException>(() => new GitHubAuthentication(new("", ""), api, store).BeginAsync(default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => new GitHubAuthentication(new("id", ""), api, store)
            .CompleteAsync(new("device", "ABCD-EFGH", 5, DateTimeOffset.UtcNow.AddMinutes(10)), cancellation.Token));
        Assert.IsNull(store.Token);
    }

    [TestMethod]
    public void ExpiredTokenIsRemoved()
    {
        using var http = new HttpClient();
        var store = new FakeGitHubCredentials { Token = new("token", DateTimeOffset.UtcNow.AddMinutes(-1)) };
        Assert.IsNull(new GitHubAuthentication(new("id", ""), new(http), store).Load());
        Assert.IsNull(store.Token);
    }

    [TestMethod]
    public async Task DeviceTokenRefreshRotatesStoredCredentialsWithoutClientSecret()
    {
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            if (request.RequestUri!.Host == "github.com")
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                StringAssert.Contains(body, "grant_type=refresh_token");
                StringAssert.Contains(body, "refresh_token=old-refresh");
                Assert.IsFalse(body.Contains("client_secret"));
                return Json("""{"access_token":"new-access","expires_in":28800,"refresh_token":"new-refresh","refresh_token_expires_in":15897600}""");
            }
            Assert.AreEqual("new-access", request.Headers.Authorization!.Parameter);
            return Json(request.RequestUri.AbsolutePath.EndsWith("pulls") ? "[]" : "{}");
        }));
        var api = new GitHubApi(http);
        var store = new FakeGitHubCredentials { Token = new("old-access", DateTimeOffset.UtcNow.AddMinutes(-1), "old-refresh", DateTimeOffset.UtcNow.AddDays(5)) };
        var vm = new GitHubViewModel(new(new("client-id", "slug"), api, store), api, new FakeGitBranchReader());
        vm.Initialize();
        Assert.IsTrue(vm.IsConnected);
        await vm.RefreshAsync(["project"], default);
        Assert.AreEqual("new-access", store.Token!.AccessToken);
        Assert.AreEqual("new-refresh", store.Token.RefreshToken);
        Assert.IsTrue(vm.IsConnected);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
}
