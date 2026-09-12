using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class McpLiveConnectionTests
{
    [TestMethod]
    public async Task ObsidianConnectsThroughProductionSetupAndTransport()
    {
        if (Environment.GetEnvironmentVariable("PI_MCP_TEST_OBSIDIAN") != "1")
        { Assert.Inconclusive("Opt-in live test: requires cached Obsidian and Pi."); return; }
        var scratch = Path.Combine(Path.GetTempPath(), "pi-obsidian-live-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(scratch, ".obsidian"));
        var previous = Environment.GetEnvironmentVariable("PI_CODING_AGENT_DIR");
        try
        {
            Environment.SetEnvironmentVariable("PI_CODING_AGENT_DIR", scratch);
            var factory = new PiProcessStartInfoFactory(new PiRuntimeOptions());
            var connection = new McpConnectionService(() => new PiRpcClient(new ProcessPiTransport(factory), TimeSpan.FromMinutes(2)), scratch);
            var store = new McpSetupService(scratch, () => true);
            using var model = new McpSetupViewModel("obsidian", null, new(null!, null!, null!, store, null, connection));
            await model.InitializeAsync();
            model.Folder = scratch;
            await model.RunAsync(true);
            StringAssert.Contains(model.Message, "Connection verified");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PI_CODING_AGENT_DIR", previous);
            Directory.Delete(scratch, true);
        }
    }
}
