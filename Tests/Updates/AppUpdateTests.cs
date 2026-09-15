using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Updates;
using PiAgentGui.Services.Updates;
using PiAgentGui.Tests.GitHub;

namespace PiAgentGui.Tests.Updates;

[TestClass]
public sealed class AppUpdateTests
{
    private static string ReleaseJson(string tag = "v1.2.1", bool prerelease = false, string host = "github.com") => JsonSerializer.Serialize(new
    {
        tag_name = tag, draft = false, prerelease,
        assets = new[] { $"PiDesktop-Setup-{tag.TrimStart('v')}-x64.exe", $"PiDesktop-Setup-{tag.TrimStart('v')}-x64.exe.sha256" }
            .Select(name => new { name, size = 100, browser_download_url = $"https://{host}/Eliaszac/pi-agent-winui/releases/download/{tag}/{name}" })
    });

    [TestMethod]
    public void SelectsNewStableVersionAndRejectsDowngradesAndForeignAssets()
    {
        using var json = JsonDocument.Parse(ReleaseJson());
        Assert.AreEqual(new Version(1, 2, 1), GitHubReleaseParser.Parse(json.RootElement, new(1, 2, 0))!.Version);
        Assert.IsNull(GitHubReleaseParser.Parse(json.RootElement, new(1, 2, 1, 0)));
        Assert.IsNull(GitHubReleaseParser.Parse(json.RootElement, new(1, 3, 0)));
        using var prerelease = JsonDocument.Parse(ReleaseJson(prerelease: true));
        Assert.IsNull(GitHubReleaseParser.Parse(prerelease.RootElement, new(1, 2, 0)));
        using var foreign = JsonDocument.Parse(ReleaseJson(host: "example.com"));
        Assert.ThrowsException<InvalidDataException>(() => GitHubReleaseParser.Parse(foreign.RootElement, new(1, 2, 0)));
    }

    [TestMethod]
    public async Task VerifiesDownloadsReusesCacheAndRejectsCorruption()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-update-test-" + Guid.NewGuid().ToString("N"));
        var data = new byte[] { 1, 2, 3, 4 };
        var hash = Convert.ToHexString(SHA256.HashData(data));
        var name = "PiDesktop-Setup-1.2.1-x64.exe";
        var downloads = 0;
        var corrupt = false;
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            Assert.IsNull(request.Headers.Authorization);
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256")) return new(HttpStatusCode.OK) { Content = new StringContent(hash + "  " + name) };
            downloads++;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(corrupt ? [9, 8, 7, 6] : data) };
        }));
        var client = new AppUpdateClient(http, directory);
        var release = new AppRelease(new(1, 2, 1), name, new("https://github.com/installer"), new("https://github.com/installer.sha256"), data.Length, "sha256:" + hash);
        try
        {
            var path = await client.DownloadAsync(release, new Progress<double>(), default);
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(path));
            await client.DownloadAsync(release, new Progress<double>(), default);
            Assert.AreEqual(1, downloads);
            File.Delete(path);
            corrupt = true;
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.DownloadAsync(release, new Progress<double>(), default));
            Assert.AreEqual(0, Directory.GetFiles(directory).Length);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task RejectsRedirectsOutsideGitHubAndMissingReleaseIsNotAnUpdate()
    {
        using var http = new HttpClient(new FakeGitHubHandler(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://example.com/payload") } }));
        var client = new AppUpdateClient(http, "unused");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.CheckAsync(new(1, 2, 0), default));
        using var missing = new HttpClient(new FakeGitHubHandler(_ => new(HttpStatusCode.NotFound)));
        Assert.IsNull(await new AppUpdateClient(missing, "unused").CheckAsync(new(1, 2, 0), default));
    }

    [TestMethod]
    public async Task UpdatePreferencesRoundTripWithoutChangingOtherSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-update-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PiAgentGui.Services.Settings.AppSettingsStore(Path.Combine(directory, "settings.json"));
            await store.SaveAsync(store.Current with { AutomaticUpdateChecks = false, AutomaticUpdateDownloads = true, Theme = 2 });
            await store.LoadAsync();
            Assert.IsFalse(store.Current.AutomaticUpdateChecks);
            Assert.IsTrue(store.Current.AutomaticUpdateDownloads);
            Assert.AreEqual(2, store.Current.Theme);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task RestartRequiresExplicitActionAndNoActiveWork()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-update-flow-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[100];
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using var http = new HttpClient(new FakeGitHubHandler(request => new(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.Host == "api.github.com" ? new StringContent(ReleaseJson("v99.0.0")) :
                request.RequestUri.AbsolutePath.EndsWith(".sha256") ? new StringContent(hash + "  PiDesktop-Setup-99.0.0-x64.exe") : new ByteArrayContent(bytes)
        }));
        try
        {
            var store = new PiAgentGui.Services.Settings.AppSettingsStore(Path.Combine(directory, "settings.json"));
            var active = true;
            var installs = 0;
            var model = new PiAgentGui.ViewModels.Settings.AppUpdatesViewModel(new AppUpdateClient(http, directory), store,
                () => active ? "Work is running" : null, _ => { installs++; return Task.CompletedTask; }, default);
            await model.CheckAsync();
            Assert.IsTrue(model.HasUpdate);
            Assert.AreEqual(0, installs);
            await model.ActAsync();
            Assert.IsTrue(model.ShowRestartNote);
            Assert.IsFalse(model.CanAct);
            await model.ActAsync();
            Assert.AreEqual(0, installs);
            active = false;
            model.RefreshAvailability();
            Assert.IsTrue(model.CanAct);
            await model.ActAsync();
            Assert.AreEqual(1, installs);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task CancelledDownloadDoesNotLeaveAnInstaller()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pi-update-cancel-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        var name = "PiDesktop-Setup-1.2.1-x64.exe";
        var bytes = new byte[100];
        using var http = new HttpClient(new FakeGitHubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256"))
                return new(HttpStatusCode.OK) { Content = new StringContent(Convert.ToHexString(SHA256.HashData(bytes)) + "  " + name) };
            cancellation.Cancel();
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }));
        try
        {
            var release = new AppRelease(new(1, 2, 1), name, new("https://github.com/installer"), new("https://github.com/installer.sha256"), 100, null);
            try { await new AppUpdateClient(http, directory).DownloadAsync(release, new Progress<double>(), cancellation.Token); Assert.Fail("Expected cancellation"); }
            catch (OperationCanceledException) { }
            Assert.AreEqual(0, Directory.GetFiles(directory).Length);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
