using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ProviderServiceTests
{
    private static ProviderService Create(FakePiTransport transport) => new(() => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), Path.GetTempPath());

    [TestMethod]
    public async Task MissingManagementIntegrationNeverSendsAModelPrompt()
    {
        var transport = new FakePiTransport { AutoReply = true };
        await using var service = Create(transport);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.RunAsync("list"));
        Assert.IsTrue(transport.Launch!.ManageProviders);
        Assert.AreEqual("", transport.Launch.SessionFile);
        Assert.IsFalse(transport.Commands.Contains("prompt"));
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public async Task AuthValueTravelsOnlyThroughExtensionReplyAndBrowserEventsKeepFlowing()
    {
        var transport = new FakePiTransport();
        await using var service = Create(transport);
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = 0;
        service.CredentialsChanged += () => changes++;
        var operation = service.RunAsync("login", "test", "api_key", (prompt, token) =>
        {
            Assert.AreEqual("secret", prompt.GetProperty("type").GetString());
            prompted.TrySetResult();
            return answer.Task.WaitAsync(token);
        }, notification => { if (notification.GetProperty("kind").GetString() == "dismiss") dismissed.TrySetResult(); });
        var discovery = await transport.NextRequestAsync();
        transport.Reply(discovery, new { commands = new[] { new { name = "pi-gui-providers" } } });
        var command = await transport.NextRequestAsync();
        Assert.IsFalse(command.GetRawText().Contains("test-secret"));
        transport.Push(JsonSerializer.Serialize(new { type = "extension_ui_request", method = "input", id = "input-1",
            title = JsonSerializer.Serialize(new { piGuiProviders = 1, promptId = "p1", type = "secret", message = "API key" }) }));
        await prompted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Notify(transport, new { piGuiProviders = 1, kind = "dismiss", promptId = "p1" });
        await dismissed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        answer.SetResult("test-secret");
        var reply = await transport.NextRequestAsync();
        Assert.AreEqual("extension_ui_response", reply.GetProperty("type").GetString());
        Assert.AreEqual("test-secret", reply.GetProperty("value").GetString());
        Notify(transport, new { piGuiProviders = 1, kind = "changed" });
        Notify(transport, new { piGuiProviders = 1, kind = "result", providers = Array.Empty<object>() });
        transport.Reply(command);
        Assert.AreEqual(0, (await operation).Count);
        Assert.AreEqual(1, changes);
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public async Task CancellationDisposesManagementProcessWhilePromptIsPending()
    {
        var transport = new FakePiTransport();
        await using var service = Create(transport);
        using var cancellation = new CancellationTokenSource();
        var operation = service.RunAsync("login", "test", "oauth", cancellationToken: cancellation.Token);
        var discovery = await transport.NextRequestAsync();
        transport.Reply(discovery, new { commands = new[] { new { name = "pi-gui-providers" } } });
        await transport.NextRequestAsync();
        cancellation.Cancel();
        try { await operation.WaitAsync(TimeSpan.FromSeconds(3)); Assert.Fail("Expected cancellation"); }
        catch (OperationCanceledException) { }
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public async Task ErrorBeforeCommandAcknowledgmentFailsPromptlyAndDoesNotRetry()
    {
        var transport = new FakePiTransport();
        await using var service = Create(transport);
        var operation = service.RunAsync("list");
        var discovery = await transport.NextRequestAsync();
        transport.Reply(discovery, new { commands = new[] { new { name = "pi-gui-providers" } } });
        await transport.NextRequestAsync();
        Notify(transport, new { piGuiProviders = 1, kind = "error", message = "Setup failed" });
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, transport.Commands.Count(command => command == "prompt"));
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public void MalformedProviderMetadataIsRejected()
    {
        using var json = JsonDocument.Parse("""[{"id":"test","name":"Test","models":null}]""");
        Assert.ThrowsException<InvalidDataException>(() => PiProviderParser.Parse(json.RootElement));
    }

    private static void Notify(FakePiTransport transport, object data) => transport.Push(JsonSerializer.Serialize(new {
        type = "extension_ui_request", method = "notify", message = JsonSerializer.Serialize(data) }));
}
