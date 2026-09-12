using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PaletteUsageTests
{
    [TestMethod]
    public async Task UsageSurvivesReloadButSearchRelevanceWinsAndUnavailableCommandsAreHidden()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pi-palette-" + Guid.NewGuid());
        var path = Path.Combine(folder, "usage.json");
        try
        {
            var store = new PaletteUsageStore(path);
            await store.RecordAsync("providers"); await store.RecordAsync("providers"); await store.RecordAsync("extensions");
            var reloaded = new PaletteUsageStore(path); await reloaded.LoadAsync();
            PaletteCommand[] commands = [new("extensions", "Extensions", "Plugins"), new("providers", "Providers", "Extensions and models", Aliases: "login"), new("hidden", "Hidden", "", Available: () => false)];
            Assert.AreEqual("providers", reloaded.Rank(commands, "")[0].Id);
            Assert.AreEqual(2, reloaded.Rank(commands, "").Count);
            Assert.AreEqual("extensions", reloaded.Rank(commands, "Extensions")[0].Id);
            Assert.AreEqual("providers", reloaded.Rank(commands, "login").Single().Id);
            Assert.AreEqual(0, reloaded.Rank(commands, "nothing matches").Count);
            await File.WriteAllTextAsync(path, "broken"); await reloaded.LoadAsync();
            Assert.AreEqual("extensions", reloaded.Rank(commands, "")[0].Id);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
