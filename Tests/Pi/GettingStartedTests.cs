using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.ViewModels.Extensions;
using PiAgentGui.ViewModels.Providers;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class GettingStartedTests
{
    private string root = "";
    [TestInitialize] public void Setup() => root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests", Guid.NewGuid().ToString("N"))).FullName;
    [TestCleanup] public void Cleanup()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PiAgentGui.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe cleanup path");
        Directory.Delete(root, true);
    }
    private static PiProvider Provider(string method) => new("example", "Example", true, method == "api_key" ? "environment" : "stored", method, method != "api_key", true, true, "Sign in", []);
    private static ExtensionsViewModel Extensions(Func<bool>? missing = null) => new([
        new("Example extension", "Author", "1", "Description", "Details", "install", "", "", new("https://example.com"), new("https://example.com"), () => ("Status", missing?.Invoke() ?? true))
    ], () => []);

    [TestMethod]
    [DataRow("api_key")]
    [DataRow("oauth")]
    public async Task MissingProviderTransitionsToOptionalExtensionsAfterConfiguration(string method)
    {
        var service = new FakeProviderService(); var providers = new ProvidersViewModel(service);
        using var model = new GettingStartedViewModel(providers, Extensions(), Path.Combine(root, "onboarding.json"));
        Assert.IsFalse(model.NeedsProvider);
        await model.InitializeAsync();
        Assert.IsTrue(model.NeedsProvider); Assert.IsTrue(model.ShowProviderSetup); Assert.IsFalse(model.ShowRecommendations);
        service.Providers = [Provider(method)]; await providers.RefreshAsync();
        Assert.IsFalse(model.NeedsProvider); Assert.IsFalse(model.ShowProviderSetup); Assert.IsTrue(model.ShowRecommendations);
        Assert.AreEqual("Optional extensions · 0/1 ready", model.ExtensionTitle);
    }

    [TestMethod]
    public async Task DismissalSurvivesRestartButDoesNotHideMissingProvider()
    {
        var service = new FakeProviderService { Providers = [Provider("oauth")] }; var providers = new ProvidersViewModel(service);
        var path = Path.Combine(root, "onboarding.json");
        using (var first = new GettingStartedViewModel(providers, Extensions(), path))
        {
            await first.InitializeAsync(); await first.DismissCommand.ExecuteAsync(); Assert.IsFalse(first.IsVisible);
        }
        using var restored = new GettingStartedViewModel(providers, Extensions(), path);
        await restored.InitializeAsync(); Assert.IsFalse(restored.IsVisible);
        service.Providers = []; await providers.RefreshAsync();
        Assert.IsTrue(restored.NeedsProvider); Assert.IsTrue(restored.IsVisible);
    }

    [TestMethod]
    public async Task FailedProviderCheckIsUnknownAndAllReadyHidesSuggestions()
    {
        var service = new FakeProviderService { Fail = true }; var providers = new ProvidersViewModel(service);
        using var model = new GettingStartedViewModel(providers, Extensions(() => false), Path.Combine(root, "onboarding.json"));
        await model.InitializeAsync();
        Assert.IsFalse(model.NeedsProvider); Assert.IsTrue(model.ShowProviderSetup);
        StringAssert.Contains(model.ProviderMessage, "Couldn't check");
        service.Fail = false; service.Providers = [Provider("api_key")]; await providers.RefreshAsync();
        Assert.IsFalse(model.IsVisible);
        Assert.AreEqual("Optional extensions · 1/1 ready", model.ExtensionTitle);
    }
}
