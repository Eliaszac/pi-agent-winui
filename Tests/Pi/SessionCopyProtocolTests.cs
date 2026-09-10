using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class SessionCopyProtocolTests
{
    [TestMethod]
    public async Task MissingIntegrationNeverFallsThroughToAModelPrompt()
    {
        var transport = new FakePiTransport { AutoReply = true };
        var launch = new PiLaunchRequest(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jsonl"));
        await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
        await session.ConnectAsync();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => session.CopySessionAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jsonl"), "Copy"));
        Assert.IsFalse(transport.Commands.Contains("prompt"));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => session.CopySessionAsync(launch.SessionFile, "Copy"));
        await session.SendAsync("Still usable");
    }

    [TestMethod]
    public async Task CopyUsesTheInternalCommandWithoutSwitchingOrStartingAnAgentRun()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PiAgentGui.CopyTest-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var launch = new PiLaunchRequest(root, Path.Combine(root, Guid.NewGuid().ToString("N") + ".jsonl"));
            var target = Path.Combine(root, Guid.NewGuid().ToString("N") + ".jsonl");
            var transport = new FakePiTransport { AutoReply = true };
            await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)));
            await session.ConnectAsync();
            transport.AutoReply = false;
            var copy = session.CopySessionAsync(target, "Quoted \"copy\" title");
            JsonElement request;
            // Startup can also query commands; drain up to this request's command query.
            do { request = await transport.NextRequestAsync(); } while (request.GetProperty("type").GetString() != "get_messages");
            request = await transport.NextRequestAsync();
            Assert.AreEqual("get_commands", request.GetProperty("type").GetString());
            transport.Reply(request, new { commands = new[] { new { name = "pi-gui-copy-session", source = "extension" } } });
            request = await transport.NextRequestAsync();
            Assert.AreEqual("prompt", request.GetProperty("type").GetString());
            var message = request.GetProperty("message").GetString()!;
            StringAssert.StartsWith(message, "/pi-gui-copy-session ");
            using var arguments = JsonDocument.Parse(message["/pi-gui-copy-session ".Length..]);
            Assert.AreEqual(target, arguments.RootElement.GetProperty("target").GetString());
            Assert.AreEqual("Quoted \"copy\" title", arguments.RootElement.GetProperty("title").GetString());
            await File.WriteAllTextAsync(target, "Created by the test transport");
            transport.Reply(request);
            await copy;
            Assert.IsFalse(transport.Commands.Contains("clone"));
            Assert.IsFalse(transport.Commands.Contains("switch_session"));
            await Assert.ThrowsExceptionAsync<IOException>(() => session.CopySessionAsync(target, "Do not replace"));
        }
        finally
        {
            if (!root.StartsWith(Path.Combine(Path.GetTempPath(), "PiAgentGui.CopyTest-"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            Directory.Delete(root, true);
        }
    }
}
