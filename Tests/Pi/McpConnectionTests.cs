using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Pi;
using System.Text.Json;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class McpConnectionTests
{
    private static McpConnectionService Create(FakePiTransport transport) => new(() => new PiRpcClient(transport, TimeSpan.FromSeconds(2)), Path.GetTempPath());

    [TestMethod]
    public async Task MissingCommandNeverFallsThroughToModel()
    {
        var transport = new FakePiTransport { AutoReply = true };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => Create(transport).RunAsync("notes", false, _ => { }, (_, _) => Task.FromResult<string?>(null), default));
        Assert.IsFalse(transport.Commands.Contains("prompt"));
        Assert.AreEqual("notes", transport.Launch!.ManageMcpServer);
        Assert.AreEqual("", transport.Launch.SessionFile);
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public async Task OAuthCallbackDoesNotBlockCompletionAndProcessIsDisposed()
    {
        var transport = new FakePiTransport();
        var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = Create(transport).RunAsync("notes", true, _ => { }, async (_, token) => { prompted.SetResult(); await Task.Delay(Timeout.Infinite, token); return null; }, default);
        transport.Reply(await transport.NextRequestAsync(), new { commands = new[] { new { name = "pi-gui-mcp-manage" } } });
        var request = await transport.NextRequestAsync();
        transport.Push("""{"type":"extension_ui_request","method":"input","id":"callback","title":"Sign in"}""");
        await prompted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        transport.Push(JsonSerializer.Serialize(new { type = "extension_ui_request", method = "notify", message = JsonSerializer.Serialize(new { piGuiMcp = 1, kind = "result", connected = true, state = "connected", toolCount = 4, message = "Connected" }) }));
        transport.Reply(request);
        Assert.IsTrue((await run.WaitAsync(TimeSpan.FromSeconds(2))).Connected);
        Assert.IsTrue(transport.Disposed);
    }

    [TestMethod]
    public async Task CancelStopsPendingOperationWithoutReplay()
    {
        var transport = new FakePiTransport();
        using var stop = new CancellationTokenSource();
        var run = Create(transport).RunAsync("notes", true, _ => { }, (_, _) => Task.FromResult<string?>(null), stop.Token);
        transport.Reply(await transport.NextRequestAsync(), new { commands = new[] { new { name = "pi-gui-mcp-manage" } } });
        await transport.NextRequestAsync();
        stop.Cancel();
        try { await run.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Fail("Expected cancellation."); }
        catch (OperationCanceledException) { }
        Assert.AreEqual(1, transport.Commands.Count(command => command == "prompt"));
        Assert.IsTrue(transport.Disposed);
    }
}
