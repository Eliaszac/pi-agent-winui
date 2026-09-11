using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class SteeringSessionTests
{
    [TestMethod]
    public async Task SteeringUsesStreamingBehaviorAndStopRecoversImagesBeforeAbort()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "steering-test.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), () => false);
        await session.ConnectAsync();
        transport.AutoReply = false;
        var image = new ChatImage("aGVsbG8=");
        var send = session.SteerAsync("Correction", [image]);
        var request = await transport.NextRequestAsync();
        while (request.GetProperty("type").GetString() != "prompt") request = await transport.NextRequestAsync();
        Assert.AreEqual("steer", request.GetProperty("streamingBehavior").GetString());
        Assert.AreEqual(image.Data, request.GetProperty("images")[0].GetProperty("data").GetString());
        transport.Reply(request); await send;
        var recovered = new TaskCompletionSource<IReadOnlyList<PendingPrompt>>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Updated += update => { if (update.RecoveredPrompts is not null) recovered.TrySetResult(update.RecoveredPrompts); };
        var stop = session.StopAsync();
        request = await transport.NextRequestAsync();
        Assert.AreEqual("clear_queue", request.GetProperty("type").GetString());
        transport.Push("""{"type":"queue_update","steering":[],"followUp":[]}""");
        transport.Reply(request, new { steering = new[] { "Correction" }, followUp = Array.Empty<string>() });
        var items = await recovered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(image, items.Single().Images.Single());
        request = await transport.NextRequestAsync();
        Assert.AreEqual("abort", request.GetProperty("type").GetString());
        transport.Reply(request); await stop;
    }

    [TestMethod]
    public async Task SteeringRejectsExtensionCommandBeforeSending()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "steering-command.jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => session.SteerAsync("/extension", []));
        Assert.AreEqual(0, transport.Commands.Count);
    }
}
