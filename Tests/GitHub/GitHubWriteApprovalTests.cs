using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.GitHub;
using PiAgentGui.Tests.Pi;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.GitHub;

namespace PiAgentGui.Tests.GitHub;

[TestClass]
public sealed class GitHubWriteApprovalTests
{
    [DataTestMethod]
    [DataRow("Allow", 1)]
    [DataRow("Block", 0)]
    [DataRow("Dismiss", 0)]
    public async Task NativeApprovalGatesWritesAndRepliesToTheOwningSession(string choice, int expectedWrites)
    {
        var writes = 0;
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            if (request.Method != HttpMethod.Get) Interlocked.Increment(ref writes);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"title\":\"Old\",\"body\":\"Body\",\"state\":\"open\",\"updated_at\":\"one\"}") };
        }));
        var api = new GitHubApi(http);
        var github = new GitHubViewModel(new(new("id", "slug"), api, new FakeGitHubCredentials { Token = new("token", DateTimeOffset.UtcNow.AddHours(1)) }), api, new FakeGitBranchReader());
        github.Initialize();
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var vm = new ConversationViewModel(session, dispatcher) { GitHub = github, ReadGitHubRepository = _ => Task.FromResult("owner/repo") };
        await vm.InitializeAsync(); dispatcher.Drain();
        using var packet = JsonDocument.Parse(JsonSerializer.Serialize(new { id = "native-request", placeholder = "{\"action\":\"update_issue\",\"number\":12,\"title\":\"New\"}" }));
        session.Emit(new() { GitHubWriteRequest = packet.RootElement.Clone() });
        for (var i = 0; i < 100 && vm.Prompts.Count == 0; i++) { dispatcher.Drain(); await Task.Delay(5); }
        var prompt = vm.Prompts.Single();
        StringAssert.Contains(prompt.Preview, "Old"); StringAssert.Contains(prompt.Preview, "New");
        Assert.AreEqual(0, writes);
        if (choice == "Dismiss") { session.Emit(new() { DismissPrompts = true }); dispatcher.Drain(); }
        else { prompt.Value = choice; await prompt.SubmitCommand.ExecuteAsync(); }
        for (var i = 0; i < 100 && session.Replies.IsEmpty; i++) { dispatcher.Drain(); await Task.Delay(5); }
        Assert.AreEqual(expectedWrites, writes);
        var reply = session.Replies.Single(); Assert.AreEqual("native-request", reply.Id);
        using var result = JsonDocument.Parse(reply.Response["value"]!.GetValue<string>());
        Assert.AreEqual(choice == "Allow", result.RootElement.TryGetProperty("success", out _));
        dispatcher.Drain(); Assert.AreEqual(0, vm.Prompts.Count);
    }
}
