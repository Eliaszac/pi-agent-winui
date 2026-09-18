using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Settings;
using PiAgentGui.Services.Settings;
using PiAgentGui.Services.Applications;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Settings;

[TestClass]
public sealed class PreferenceAdditionsTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PiPreferences-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [TestMethod]
    public async Task OldSettingsKeepDefaultsAndNewPreferencesRoundTrip()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, "{\"ResumeConversation\":true}");
        var store = new AppSettingsStore(path);
        await store.LoadAsync();
        Assert.AreEqual(new AppPreferences(ResumeConversation: true), store.Current);
        var expected = store.Current with { Theme = 2, ConversationTextSize = 19, CodeTextSize = 16, ControlEnterToSend = true,
            TerminalTextSize = 18, TerminalScrollback = 10000, CompletionAudio = false };
        await store.SaveAsync(expected);
        var reload = new AppSettingsStore(path); await reload.LoadAsync();
        Assert.AreEqual(expected, reload.Current);
    }

    [TestMethod]
    public async Task TerminalSettingsAreBoundedAndResetWithoutChangingAudio()
    {
        var store = new AppSettingsStore(Path.Combine(directory, "settings.json"));
        var model = new PiAgentGui.ViewModels.Settings.SettingsViewModel(store);
        await model.SetCompletionAudioAsync(false);
        await model.SetTerminalSizeAsync(double.NaN);
        Assert.AreEqual(12d, model.TerminalTextSize);
        await model.SetTerminalSizeAsync(100);
        Assert.AreEqual(24d, model.TerminalTextSize);
        await model.SetTerminalScrollbackAsync(-1);
        Assert.AreEqual(0, model.TerminalScrollback);
        await model.SetTerminalScrollbackAsync(int.MaxValue);
        Assert.AreEqual(50000, model.TerminalScrollback);
        await model.RestoreDefaultsAsync(SettingsDefaultsCategory.Terminal);
        Assert.AreEqual(12d, model.TerminalTextSize);
        Assert.AreEqual(5000, model.TerminalScrollback);
        Assert.IsFalse(model.CompletionAudio);
        model.Query = "scrollback";
        Assert.IsTrue(model.Terminal.Visible);
        model.Query = "sound";
        Assert.IsTrue(model.Conversation.Visible);
    }

    [TestMethod]
    public void InvalidSizesAndThemesAreNormalized()
    {
        var normalized = new AppPreferences(Theme: 99, ConversationTextSize: 500, CodeTextSize: -1).Normalize();
        Assert.AreEqual(0, normalized.Theme); Assert.AreEqual(22d, normalized.ConversationTextSize); Assert.AreEqual(10d, normalized.CodeTextSize);
        Assert.AreEqual(15d, new AppPreferences(ConversationTextSize: double.NaN).Normalize().ConversationTextSize);
    }

    [TestMethod]
    public void SendShortcutCanBeReversed()
    {
        Assert.IsTrue(ComposerEnterBehavior.Sends(false, false)); Assert.IsFalse(ComposerEnterBehavior.Sends(true, false));
        Assert.IsFalse(ComposerEnterBehavior.Sends(false, true)); Assert.IsTrue(ComposerEnterBehavior.Sends(true, true));
    }

    [TestMethod]
    public void EditorCanReturnToSystemDefault()
    {
        var store = new OpenInPreferenceStore(Path.Combine(directory, "editor.json"));
        store.Save("vscode"); Assert.AreEqual("vscode", store.Read());
        store.Save(null); Assert.IsNull(store.Read());
    }

    [TestMethod]
    public async Task PaletteResetClearsDiskAndInMemoryRankingAndAllowsLearningAgain()
    {
        var path = Path.Combine(directory, "palette.json");
        var store = new PaletteUsageStore(path);
        PaletteCommand[] commands = [new("first", "First", ""), new("second", "Second", "")];
        await store.RecordAsync("second"); Assert.AreEqual("second", store.Rank(commands, "")[0].Id);
        await store.ClearAsync(); Assert.AreEqual("first", store.Rank(commands, "")[0].Id);
        var reload = new PaletteUsageStore(path); await reload.LoadAsync();
        Assert.AreEqual("first", reload.Rank(commands, "")[0].Id);
        await store.RecordAsync("second"); Assert.AreEqual("second", store.Rank(commands, "")[0].Id);
    }

    [TestMethod]
    public async Task EditorSettingsExposeMissingPreferenceAndUpdateWithoutLaunchingEditor()
    {
        var store = new OpenInPreferenceStore(Path.Combine(directory, "editor.json"));
        store.Save("missing");
        var model = new PiAgentGui.ViewModels.Applications.OpenInViewModel(new PreferenceApplicationLocator(), store, new ProjectApplicationLauncher());
        await model.RefreshEditorsAsync();
        Assert.IsFalse(model.EditorOptions.Single(option => option.Id == "missing").Available);
        await model.SetPreferredEditorAsync("vscode");
        Assert.AreEqual("vscode", store.Read()); Assert.AreEqual("vscode", model.PreferredEditor);
        Assert.IsFalse(model.EditorOptions.Any(option => option.Id == "missing"));
        await model.SetPreferredEditorAsync(null);
        Assert.IsNull(model.PreferredEditor); Assert.IsNull(store.Read());
    }
}
