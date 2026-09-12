using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ExecutionTargetSessionTests
{
    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ConnectionRequiresTheMatchingTargetAcknowledgement(bool extensionAvailable, bool acknowledged)
    {
        var target = new ExecutionTarget { Id = Guid.NewGuid(), Name = "Ubuntu", Kind = "wsl", Host = "Ubuntu", Path = "/workspace" };
        var transport = new FakePiTransport { AutoReply = true, StartTurnOnPrompt = false,
            ExtensionCommands = extensionAvailable ? """[{"name":"pi-gui-target-check"}]""" : "[]",
            TargetReadyId = acknowledged ? target.Id.ToString() : Guid.NewGuid().ToString() };
        var directory = Path.Combine(Path.GetTempPath(), "pi-target-session-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var launch = new PiLaunchRequest(target.Path, Path.Combine(directory, "session.jsonl"), Target: target);
            await using var session = new ConversationSession(launch, () => new PiRpcClient(transport, TimeSpan.FromSeconds(3)), () => false);
            if (extensionAvailable && acknowledged)
            {
                await session.ConnectAsync();
                Assert.IsFalse(transport.Disposed);
                Assert.IsTrue(transport.Commands.Contains("get_messages"));
            }
            else
            {
                await Assert.ThrowsExceptionAsync<IOException>(() => session.ConnectAsync());
                Assert.IsTrue(transport.Disposed);
                Assert.IsFalse(transport.Commands.Contains("get_messages"));
            }
            Assert.AreEqual(extensionAvailable ? 1 : 0, transport.Commands.Count(command => command == "prompt"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
