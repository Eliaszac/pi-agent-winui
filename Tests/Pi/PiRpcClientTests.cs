using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiRpcClientTests
{
    private static readonly PiLaunchRequest Launch = new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "test.jsonl"));

    [TestMethod]
    public async Task CorrelatesOutOfOrderRepliesWhileDeliveringEvents()
    {
        var transport = new FakePiTransport();
        await using var client = new PiRpcClient(transport, TimeSpan.FromSeconds(3));
        await client.StartAsync(Launch);
        var eventReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.EventReceived += packet => eventReceived.TrySetResult(PiJson.Text(packet, "type"));
        var first = client.RequestAsync("get_state");
        var second = client.RequestAsync("get_messages");
        var request1 = await transport.NextRequestAsync();
        var request2 = await transport.NextRequestAsync();
        transport.Push("{\"type\":\"agent_start\"}");
        transport.Reply(request2, new { marker = "second" });
        transport.Reply(request1, new { marker = "first" });
        Assert.AreEqual("first", PiJson.Text(PiJson.Field(await first, "data"), "marker"));
        Assert.AreEqual("second", PiJson.Text(PiJson.Field(await second, "data"), "marker"));
        Assert.AreEqual("agent_start", await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [TestMethod]
    public async Task RejectionDoesNotDisconnectOrReplayCommand()
    {
        var transport = new FakePiTransport();
        await using var client = new PiRpcClient(transport, TimeSpan.FromSeconds(3));
        await client.StartAsync(Launch);
        var prompt = client.RequestAsync("prompt");
        transport.Reply(await transport.NextRequestAsync(), success: false);
        await Assert.ThrowsExceptionAsync<PiCommandException>(() => prompt);
        var state = client.RequestAsync("get_state");
        transport.Reply(await transport.NextRequestAsync());
        await state;
        Assert.AreEqual(1, transport.Commands.Count(command => command == "prompt"));
        Assert.IsFalse(transport.Disposed);
    }

    [TestMethod]
    public async Task ExitFailsPendingRequestsPromptly()
    {
        var transport = new FakePiTransport();
        await using var client = new PiRpcClient(transport, TimeSpan.FromMinutes(1));
        await client.StartAsync(Launch);
        var waiting = client.RequestAsync("prompt");
        await transport.NextRequestAsync();
        transport.Exit();
        await Assert.ThrowsExceptionAsync<IOException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(3)));
        await Assert.ThrowsExceptionAsync<IOException>(() => client.RequestAsync("prompt"));
        Assert.AreEqual(1, transport.Commands.Count);
    }

    [TestMethod]
    public async Task InvalidCorrelatedResponseFailsWithoutWaitingForTimeout()
    {
        var transport = new FakePiTransport();
        await using var client = new PiRpcClient(transport, TimeSpan.FromMinutes(1));
        await client.StartAsync(Launch);
        var waiting = client.RequestAsync("get_state");
        var packet = await transport.NextRequestAsync();
        transport.Push($$"""{"type":"response","id":"{{PiJson.Text(packet, "id")}}","command":42,"success":true}""");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [TestMethod]
    public async Task TimeoutIsUncertainAndNeverResends()
    {
        var transport = new FakePiTransport();
        await using var client = new PiRpcClient(transport, TimeSpan.FromMilliseconds(50));
        await client.StartAsync(Launch);
        var exception = await Assert.ThrowsExceptionAsync<TimeoutException>(() => client.RequestAsync("prompt"));
        StringAssert.Contains(exception.Message, "uncertain");
        Assert.AreEqual(1, transport.Commands.Count);
    }

    [TestMethod]
    public async Task DisposeUnblocksPendingRequest()
    {
        var transport = new FakePiTransport();
        var client = new PiRpcClient(transport, TimeSpan.FromMinutes(1));
        await client.StartAsync(Launch);
        var waiting = client.RequestAsync("prompt");
        await transport.NextRequestAsync();
        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsExceptionAsync<IOException>(() => waiting);
        Assert.IsTrue(transport.Disposed);
    }
}
