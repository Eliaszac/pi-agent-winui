using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Settings;
using PiAgentGui.Services.Settings;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Tests.Settings;

[TestClass]
public sealed class TextWeightTests
{
    [TestMethod]
    public async Task WeightPersistsWithoutChangingSizeAndInvalidValuesUseSemibold()
    {
        var directory = Path.Combine(Path.GetTempPath(), "weight-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(directory, "settings.json");
        try
        {
            var store = new AppSettingsStore(file);
            var model = new SettingsViewModel(store);
            await model.SetTextWeightAsync(2);
            var reloaded = new AppSettingsStore(file);
            await reloaded.LoadAsync();
            Assert.AreEqual(600, reloaded.Current.ConversationTextWeight);
            Assert.AreEqual(15d, reloaded.Current.ConversationTextSize);
            Assert.AreEqual(12d, reloaded.Current.CodeTextSize);
            Assert.AreEqual(600, new AppPreferences(ConversationTextWeight: 999).Normalize().ConversationTextWeight);
            Assert.AreEqual(600, new AppPreferences().ConversationTextWeight);
            Assert.AreEqual(400, new AppPreferences(ConversationTextWeight: 400).Normalize().ConversationTextWeight);
            Assert.AreEqual(600, System.Text.Json.JsonSerializer.Deserialize<AppPreferences>("{}")!.ConversationTextWeight);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
