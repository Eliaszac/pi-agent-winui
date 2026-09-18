using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Settings;
using PiAgentGui.Services.Applications;
using PiAgentGui.Services.Settings;
using PiAgentGui.Services.Updates;
using PiAgentGui.Tests.GitHub;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Tests.Settings;

[TestClass]
public sealed class SettingsDefaultsTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PiDefaultsTests-" + Guid.NewGuid().ToString("N"));
    private static readonly AppPreferences Customized = new(true, false, DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
        Theme: 2, ConversationTextSize: 20, CodeTextSize: 18, ControlEnterToSend: true, ConversationTextWeight: 400,
        TerminalTextSize: 20, TerminalScrollback: 12000, CompletionAudio: false,
        AutomaticUpdateChecks: false, AutomaticUpdateDownloads: true);

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [TestMethod]
    [DataRow(SettingsDefaultsCategory.Appearance)]
    [DataRow(SettingsDefaultsCategory.Conversation)]
    [DataRow(SettingsDefaultsCategory.Terminal)]
    [DataRow(SettingsDefaultsCategory.General)]
    [DataRow(SettingsDefaultsCategory.LocalUsage)]
    [DataRow(SettingsDefaultsCategory.Updates)]
    public async Task RestorePersistsOnlyCategoryPreferencesAndPreservesUserFiles(SettingsDefaultsCategory category)
    {
        var path = Path.Combine(directory, "settings.json");
        var store = new AppSettingsStore(path);
        await store.SaveAsync(Customized);
        var editor = new OpenInPreferenceStore(Path.Combine(directory, "open-in.json"));
        editor.Save("vscode");
        string[] preservedFiles = ["projects.json", "conversation.jsonl", "source.cs", "usage-history.json", "command-usage.json"];
        foreach (var file in preservedFiles) await File.WriteAllTextAsync(Path.Combine(directory, file), "preserve " + file);
        var model = new SettingsViewModel(store)
        {
            RestoreDefaultEditor = () => { editor.Save(null); return Task.CompletedTask; }
        };

        await model.RestoreDefaultsAsync(category);

        var expected = category switch
        {
            SettingsDefaultsCategory.Appearance => Customized with { Theme = 0, ConversationTextSize = 15, CodeTextSize = 12, ConversationTextWeight = 600 },
            SettingsDefaultsCategory.Conversation => Customized with { ControlEnterToSend = false, CompletionAudio = true },
            SettingsDefaultsCategory.Terminal => Customized with { TerminalTextSize = 12, TerminalScrollback = 5000 },
            SettingsDefaultsCategory.General => Customized with { ResumeConversation = false },
            SettingsDefaultsCategory.LocalUsage => Customized with { ShowLocalUsage = true },
            SettingsDefaultsCategory.Updates => Customized with { AutomaticUpdateChecks = true, AutomaticUpdateDownloads = false },
            _ => throw new AssertFailedException("Unexpected test category")
        };
        Assert.AreEqual(expected, store.Current);
        var reloaded = new AppSettingsStore(path);
        await reloaded.LoadAsync();
        Assert.AreEqual(expected, reloaded.Current);
        Assert.AreEqual(category == SettingsDefaultsCategory.General ? null : "vscode", editor.Read());
        Assert.AreEqual(SettingsFeedbackKind.Success, model.FeedbackKind);
        Assert.IsTrue(model.CanEdit);
        foreach (var file in preservedFiles)
            Assert.AreEqual("preserve " + file, await File.ReadAllTextAsync(Path.Combine(directory, file)));
    }

    [TestMethod]
    public async Task FailedPreferenceSaveKeepsPreviousValuesAndDoesNotResetEditor()
    {
        var path = Path.Combine(directory, "settings.json");
        var store = new AppSettingsStore(path);
        await store.SaveAsync(Customized);
        File.Delete(path);
        Directory.CreateDirectory(path);
        var editorCalled = false;
        var model = new SettingsViewModel(store)
        {
            RestoreDefaultEditor = () => { editorCalled = true; return Task.CompletedTask; }
        };
        await model.RestoreDefaultsAsync(SettingsDefaultsCategory.General);
        Assert.AreEqual(Customized, store.Current);
        Assert.IsFalse(editorCalled);
        Assert.AreEqual(SettingsFeedbackKind.Error, model.FeedbackKind);
        Assert.IsTrue(model.CanEdit);
    }

    [TestMethod]
    public async Task GeneralReportsPartialSaveAndCanBeRetriedWithoutChangingOtherCategories()
    {
        var store = new AppSettingsStore(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(Customized);
        var fail = true;
        var model = new SettingsViewModel(store)
        {
            RestoreDefaultEditor = () => fail ? Task.FromException(new IOException("Editor preference is locked.")) : Task.CompletedTask
        };
        await model.RestoreDefaultsAsync(SettingsDefaultsCategory.General);
        Assert.AreEqual(Customized with { ResumeConversation = false }, store.Current);
        Assert.AreEqual(SettingsFeedbackKind.Error, model.FeedbackKind);
        StringAssert.Contains(model.Message, "Startup defaults were saved");
        Assert.IsTrue(model.CanEdit);
        fail = false;
        await model.RestoreDefaultsAsync(SettingsDefaultsCategory.General);
        Assert.AreEqual(SettingsFeedbackKind.Success, model.FeedbackKind);
    }

    [TestMethod]
    public async Task RestoreDoesNotRunTwiceWhileEditorSaveIsPending()
    {
        var store = new AppSettingsStore(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(Customized);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new SettingsViewModel(store)
        {
            RestoreDefaultEditor = async () => { entered.SetResult(); await finish.Task; }
        };
        var pending = model.RestoreDefaultsAsync(SettingsDefaultsCategory.General);
        await entered.Task;
        try
        {
            Assert.IsTrue(model.Busy);
            await model.RestoreDefaultsAsync(SettingsDefaultsCategory.Appearance);
            Assert.AreEqual(2, store.Current.Theme);
        }
        finally { finish.SetResult(); await pending; }
        Assert.IsTrue(model.CanEdit);
    }

    [TestMethod]
    public async Task UpdatesRestoreRefreshesTogglesWithoutNetworkOrInstallation()
    {
        var store = new AppSettingsStore(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(Customized);
        var requests = 0;
        var installs = 0;
        using var http = new HttpClient(new FakeGitHubHandler(_ => { requests++; return new(HttpStatusCode.NotFound); }));
        var updates = new AppUpdatesViewModel(new AppUpdateClient(http, Path.Combine(directory, "updates")), store,
            () => null, _ => { installs++; return Task.CompletedTask; }, CancellationToken.None);
        var changes = new List<string?>();
        updates.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var model = new SettingsViewModel(store) { AppUpdates = updates };
        await model.RestoreDefaultsAsync(SettingsDefaultsCategory.Updates);
        Assert.IsTrue(updates.AutomaticChecks);
        Assert.IsFalse(updates.AutomaticDownloads);
        CollectionAssert.Contains(changes, nameof(updates.AutomaticChecks));
        CollectionAssert.Contains(changes, nameof(updates.AutomaticDownloads));
        Assert.AreEqual(0, requests);
        Assert.AreEqual(0, installs);
        Assert.AreEqual("Check for a newer version", updates.Title);
    }

    [TestMethod]
    public async Task RestoreWaitsForInFlightUpdatePreferenceSave()
    {
        var store = new AppSettingsStore(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(Customized);
        using var http = new HttpClient(new FakeGitHubHandler(_ => throw new AssertFailedException("No network request expected.")));
        var updates = new AppUpdatesViewModel(new AppUpdateClient(http, directory), store,
            () => null, _ => Task.CompletedTask, CancellationToken.None);
        var model = new SettingsViewModel(store) { AppUpdates = updates };
        Task? restore = null;
        updates.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(updates.SavingPreferences) && updates.SavingPreferences)
                restore = model.RestoreDefaultsAsync(SettingsDefaultsCategory.Appearance);
        };
        await updates.SetPreferencesAsync(true, false);
        Assert.IsNotNull(restore);
        await restore;
        Assert.AreEqual(Customized.Theme, store.Current.Theme);
        Assert.AreEqual(SettingsFeedbackKind.Warning, model.FeedbackKind);
        Assert.IsTrue(updates.CanEditPreferences);
    }

    [TestMethod]
    public async Task MissingEditorServiceDoesNotPartiallyRestoreGeneral()
    {
        var store = new AppSettingsStore(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(Customized);
        var model = new SettingsViewModel(store);
        await model.RestoreDefaultsAsync(SettingsDefaultsCategory.General);
        Assert.AreEqual(Customized, store.Current);
        Assert.AreEqual(SettingsFeedbackKind.Error, model.FeedbackKind);
        Assert.IsTrue(model.CanEdit);
    }

    [TestMethod]
    public void SearchFindsRestoreActionsWithoutOfferingDataDeletionAsDefaults()
    {
        var model = new SettingsViewModel(new AppSettingsStore(Path.Combine(directory, "settings.json")));
        model.Query = "restore defaults";
        Assert.AreEqual(6, model.Categories.Count(category => category.Visible));
        Assert.IsFalse(model.Data.Visible || model.Storage.Visible || model.About.Visible);
        foreach (var category in model.Categories.Where(category => category.Visible))
        {
            Assert.IsTrue(category.Items["Defaults"].Visible);
            Assert.AreEqual(1, category.Entries.Count(entry => entry.Visible));
        }
    }
}
