using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Settings;
using PiAgentGui.Services.Settings;
using PiAgentGui.ViewModels.Settings;

namespace PiAgentGui.Tests.Settings;

[TestClass]
public sealed class SettingsDiagnosticSummaryTests
{
    [TestMethod]
    public void SummaryUsesAnExplicitFieldAllowlist()
    {
        var report = SettingsDiagnosticSummary.Create(new());
        var fields = report.Split(Environment.NewLine).Where(line => line.Contains(':'))
            .Select(line => line[..line.IndexOf(':')]).ToArray();
        string[] expected =
        [
            "Version", "Windows version", "OS architecture", "Process architecture", ".NET runtime",
            "Theme", "Conversation text size", "Code text size", "Response text weight", "Send shortcut",
            "Completion sound", "Terminal text size", "Terminal scrollback lines", "Startup page",
            "Show local usage", "Automatic update checks", "Automatic update downloads", "Scope", "Excluded"
        ];
        CollectionAssert.AreEqual(expected, fields);
        StringAssert.StartsWith(report, "Pi desktop diagnostic summary (format 1)");
        StringAssert.Contains(report, "Version: " + ApplicationIdentity.Version);
        StringAssert.Contains(report, "Pi and remote targets were not probed.");
        StringAssert.Contains(report, "credentials, conversations, project details, paths, usage history and logs.");
    }

    [TestMethod]
    public void SummaryIncludesOnlyPreferenceValuesAndOmitsUsageResetHistory()
    {
        var settings = new AppPreferences(true, false, DateTimeOffset.Parse("2042-05-06T07:08:09Z"),
            Theme: 2, ConversationTextSize: 18.5, CodeTextSize: 13.5, ControlEnterToSend: true, ConversationTextWeight: 400,
            TerminalTextSize: 16, TerminalScrollback: 9000, CompletionAudio: false,
            AutomaticUpdateChecks: false, AutomaticUpdateDownloads: true);
        var report = SettingsDiagnosticSummary.Create(settings);
        foreach (var expected in new[]
        {
            "Theme: Dark", "Conversation text size: 18.5", "Code text size: 13.5", "Response text weight: Regular",
            "Send shortcut: Ctrl + Enter", "Completion sound: Off", "Terminal text size: 16", "Terminal scrollback lines: 9000",
            "Startup page: Most recent conversation", "Show local usage: Off", "Automatic update checks: Off", "Automatic update downloads: On"
        }) StringAssert.Contains(report, expected);
        Assert.IsFalse(report.Contains("2042", StringComparison.Ordinal));
        Assert.IsFalse(report.Contains(nameof(AppPreferences.UsageResetAt), StringComparison.Ordinal));
        Assert.AreEqual(report, SettingsDiagnosticSummary.Create(settings with { UsageResetAt = null }));
    }

    [TestMethod]
    public void SummaryFormatsNumbersConsistentlyAcrossCultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            var report = SettingsDiagnosticSummary.Create(new(ConversationTextSize: 15.5));
            StringAssert.Contains(report, "Conversation text size: 15.5");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    public void CreatingPreviewDoesNotReadOrCreateTheSettingsFileOrChangeFeedback()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PiDiagnosticTests-" + Guid.NewGuid().ToString("N"));
        var model = new SettingsViewModel(new AppSettingsStore(Path.Combine(directory, "settings.json")));
        model.SetFeedback("Existing feedback", SettingsFeedbackKind.Warning);
        var report = model.CreateDiagnosticSummary();
        Assert.IsFalse(Directory.Exists(directory));
        Assert.AreEqual("Existing feedback", model.Message);
        Assert.AreEqual(SettingsFeedbackKind.Warning, model.FeedbackKind);
        StringAssert.Contains(report, "Theme: System");
        model.Query = "copy diagnostic";
        Assert.IsTrue(model.About.Items["Diagnostics"].Visible);
        Assert.AreEqual(1, model.Categories.Count(category => category.Visible));
    }

    [TestMethod]
    public async Task CapturedPreviewDoesNotChangeWhenPreferencesChange()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PiDiagnosticTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var model = new SettingsViewModel(new AppSettingsStore(Path.Combine(directory, "settings.json")));
            var captured = model.CreateDiagnosticSummary();
            await model.SetThemeAsync(2);
            StringAssert.Contains(captured, "Theme: System");
            StringAssert.Contains(model.CreateDiagnosticSummary(), "Theme: Dark");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
