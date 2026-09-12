using System.Net;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Providers;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class OllamaTests
{
    private string directory = null!;
    private OllamaModelImporter importer = null!;
    private static readonly OllamaModel Model = new("qwen3:0.6b", true, false, true, 40960);
    private static readonly Uri Local = OllamaEndpoint.Parse(OllamaEndpoint.Local);
    private const string Tags = """{"models":[{"name":"qwen3:0.6b"},{"name":"embedding"}]}""";
    private const string Details = """{"capabilities":["completion","tools","thinking"],"model_info":{"general.architecture":"qwen3","qwen3.context_length":40960}}""";

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "PiOllamaTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        importer = new(directory);
    }
    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, true);

    [TestMethod]
    public void EndpointNormalizationPreservesProxyPathsAndRejectsCredentials()
    {
        Assert.AreEqual(Local, OllamaEndpoint.Parse("http://LOCALHOST:11434/v1/"));
        Assert.AreEqual("https://server.example/ollama/api/tags", new Uri(OllamaEndpoint.Parse("https://server.example/ollama/v1"), "api/tags").AbsoluteUri);
        foreach (var input in new[] { "file:///tmp", "http://user:password@server", "https://server/?key=secret", "https://server/#fragment", "server:11434" })
            Assert.ThrowsException<ArgumentException>(() => OllamaEndpoint.Parse(input));
    }

    [TestMethod]
    public async Task DiscoveryReadsOnlyMetadataAndExcludesEmbeddingModels()
    {
        using var handler = new FakeOllamaHandler(request => Reply(request.RequestUri!.AbsolutePath == "/api/tags" ? Tags : Details));
        using var http = new HttpClient(handler);
        var client = new OllamaClient(http);
        Assert.AreEqual(2, (await client.ListAsync(Local)).Count);
        var model = await client.InspectAsync(Local, Model.Id);
        Assert.IsTrue(model.Tools);
        Assert.IsTrue(model.Thinking);
        Assert.AreEqual(40960, model.MaximumContext);
        CollectionAssert.AreEqual(new[] { "GET /api/tags", "POST /api/show" }, handler.Requests);
        using var embeddingHandler = new FakeOllamaHandler(_ => Reply("""{"capabilities":["embedding"]}"""));
        using var embeddingHttp = new HttpClient(embeddingHandler);
        Assert.IsFalse((await new OllamaClient(embeddingHttp).InspectAsync(Local, "embedding")).Tools);
    }

    [TestMethod]
    public async Task InvalidInventoryAndOversizedResponseAreRejected()
    {
        using var handler = new FakeOllamaHandler(_ => Reply("""{"models":[{"name":null}]}"""));
        using var http = new HttpClient(handler);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new OllamaClient(http).ListAsync(Local));
        using var bigHandler = new FakeOllamaHandler(_ => Reply(new string(' ', 2 * 1024 * 1024 + 1)));
        using var bigHttp = new HttpClient(bigHandler);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => new OllamaClient(bigHttp).ListAsync(Local));
    }

    [TestMethod]
    public async Task ImportPreservesUnrelatedConfigurationAndClampsToModelLimit()
    {
        var path = Path.Combine(directory, "models.json");
        await File.WriteAllTextAsync(path, """{"custom":true,"providers":{"other":{"baseUrl":"https://example.test","apiKey":"untouched","models":[{"id":"existing"}]}}}""");
        await importer.ImportAsync(Local, [Model], 64000);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.IsTrue(root["custom"]!.GetValue<bool>());
        Assert.AreEqual("untouched", root["providers"]!["other"]!["apiKey"]!.GetValue<string>());
        var provider = root["providers"]!["ollama"]!;
        Assert.AreEqual("http://localhost:11434/v1", provider["baseUrl"]!.GetValue<string>());
        Assert.AreEqual("ollama", provider["apiKey"]!.GetValue<string>());
        Assert.AreEqual(40960, provider["models"]![0]!["contextWindow"]!.GetValue<int>());
        Assert.IsFalse(provider["models"]![0]!["compat"]!["supportsDeveloperRole"]!.GetValue<bool>());
    }

    [TestMethod]
    public async Task RemoteImportsUseIndependentStableProviderIds()
    {
        var remote = OllamaEndpoint.Parse("http://workstation:11434");
        await importer.ImportAsync(Local, [Model], 32768);
        await importer.ImportAsync(remote, [Model], 32768);
        var registrations = await importer.ReadAsync();
        Assert.AreEqual(2, registrations.Count);
        Assert.AreNotEqual(registrations[0].ProviderId, registrations[1].ProviderId);
        Assert.AreEqual(OllamaEndpoint.ProviderId(remote), registrations.Single(item => item.Endpoint == remote).ProviderId);
    }

    [TestMethod]
    public async Task ExistingModelAndEndpointAreNeverReplaced()
    {
        await importer.ImportAsync(Local, [Model], 32768);
        var path = Path.Combine(directory, "models.json");
        var before = await File.ReadAllTextAsync(path);
        await Assert.ThrowsExceptionAsync<IOException>(() => importer.ImportAsync(Local, [Model with { Id = "new-model" }, Model], 4096));
        Assert.AreEqual(before, await File.ReadAllTextAsync(path));
        await importer.ImportAsync(Local, [Model with { Id = "new-model" }], 4096);
        Assert.AreEqual(2, (await importer.ReadAsync()).Single().ModelIds.Count);
    }

    [TestMethod]
    public async Task MalformedConfigurationAndUnsupportedModelsNeverWrite()
    {
        var path = Path.Combine(directory, "models.json");
        await File.WriteAllTextAsync(path, "broken");
        try { await importer.ImportAsync(Local, [Model], 4096); Assert.Fail("Expected malformed configuration to be rejected"); }
        catch (System.Text.Json.JsonException) { }
        Assert.AreEqual("broken", await File.ReadAllTextAsync(path));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => importer.ImportAsync(Local, [Model with { Tools = false }], 4096));
        Assert.AreEqual("broken", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task AutoImportRefreshesOnceAndNeverContactsAnUnsavedAddress()
    {
        using var handler = new FakeOllamaHandler(request => Reply(request.Method == HttpMethod.Get ? """{"models":[{"name":"qwen3:0.6b"}]}""" : Details));
        using var http = new HttpClient(handler);
        var changes = 0;
        var vm = new OllamaSetupViewModel(new(http), importer, () => changes++);
        await vm.SyncKnownAsync();
        Assert.IsTrue(vm.Models.Single().Imported);
        Assert.AreEqual(1, changes);
        var before = await File.ReadAllTextAsync(Path.Combine(directory, "models.json"));
        vm.Address = "http://remote:11434";
        Assert.AreEqual(0, vm.Models.Count);
        await vm.SyncKnownAsync();
        Assert.AreEqual(1, changes);
        Assert.AreEqual(before, await File.ReadAllTextAsync(Path.Combine(directory, "models.json")));
        Assert.AreEqual(1, (await importer.KnownEndpointsAsync()).Count);
        await vm.ConnectAsync();
        Assert.AreEqual(2, changes);
        Assert.AreEqual(2, (await importer.ReadAsync()).Count);
    }

    [TestMethod]
    public async Task UnsupportedModelsAreNotImportedOrExecuted()
    {
        using var handler = new FakeOllamaHandler(request => Reply(request.Method == HttpMethod.Get ? Tags : """{"capabilities":["embedding"]}"""));
        using var http = new HttpClient(handler);
        var vm = new OllamaSetupViewModel(new(http), importer, () => Assert.Fail("Detection must not import"));
        await vm.SyncKnownAsync();
        Assert.AreEqual(2, vm.LocalModelCount);
        Assert.IsFalse(File.Exists(Path.Combine(directory, "models.json")));
        Assert.IsTrue(handler.Requests.All(request => request is "GET /api/tags" or "GET /api/ps" or "POST /api/show"));
    }

    [TestMethod]
    public async Task EmptyRemoteIsRememberedAndLaterModelsAreImportedAutomatically()
    {
        var installed = false;
        using var handler = new FakeOllamaHandler(request => Reply(request.Method == HttpMethod.Post ? Details
            : installed && request.RequestUri!.Host == "remote" ? """{"models":[{"name":"qwen3:0.6b"}]}""" : """{"models":[]}"""));
        using var http = new HttpClient(handler);
        var changes = 0;
        var vm = new OllamaSetupViewModel(new(http), importer, () => changes++) { Address = "http://remote:11434" };
        await vm.ConnectAsync();
        Assert.AreEqual(2, (await importer.KnownEndpointsAsync()).Count);
        Assert.AreEqual(0, (await importer.ReadAsync()).Count);
        installed = true;
        await vm.SyncKnownAsync();
        Assert.AreEqual(1, changes);
        Assert.AreEqual("remote", (await importer.ReadAsync()).Single().Endpoint.Host);
        installed = false;
        await vm.SyncKnownAsync();
        Assert.AreEqual(1, (await importer.ReadAsync()).Single().ModelIds.Count);
    }

    [TestMethod]
    public async Task LoadedContextWinsOverModelSettingAndIsCappedAtMaximum()
    {
        using var handler = new FakeOllamaHandler(request => Reply(request.RequestUri!.AbsolutePath switch
        {
            "/api/tags" => """{"models":[{"name":"qwen3:0.6b"}]}""",
            "/api/ps" => """{"models":[{"name":"qwen3:0.6b","context_length":65536}]}""",
            _ => """{"capabilities":["completion","tools"],"parameters":"num_ctx 8192","model_info":{"general.architecture":"qwen3","qwen3.context_length":40960}}"""
        }));
        using var http = new HttpClient(handler);
        var result = await new OllamaModelSync(new(http), importer).SyncAsync(Local, false);
        Assert.AreEqual(40960, result.NewModels.Single().ImportContext);
        Assert.AreEqual("loaded model", result.NewModels.Single().ContextSource);
        var path = Path.Combine(directory, "models.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.AreEqual(40960, root["providers"]!["ollama"]!["models"]![0]!["contextWindow"]!.GetValue<int>());
        root["providers"]!["ollama"]!["models"]![0]!["contextWindow"] = 8192;
        await File.WriteAllTextAsync(path, root.ToJsonString());
        var before = await File.ReadAllTextAsync(path);
        Assert.AreEqual(0, (await importer.ImportNewAsync(Local, [Model])).Added);
        Assert.AreEqual(before, await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task MissingRuntimeContextUsesModelSettingOrExplicitFallback()
    {
        Assert.AreEqual(4096, Model.ImportContext);
        Assert.AreEqual("fallback", Model.ContextSource);
        Assert.AreEqual(8192, OllamaContext.ReadParameter("temperature 0.5\nnum_ctx 8192"));
        Assert.IsNull(OllamaContext.ReadParameter("num_ctx invalid"));
        using var handler = new FakeOllamaHandler(_ => Reply("""{"capabilities":["completion","tools"],"parameters":"num_ctx 16384"}"""));
        using var http = new HttpClient(handler);
        var model = await new OllamaClient(http).InspectAsync(Local, Model.Id);
        Assert.AreEqual(16384, model.ImportContext);
        Assert.AreEqual("model setting", model.ContextSource);
    }

    [TestMethod]
    public async Task SmallContextBudgetsAreScopedAndPreserveExplicitSettings()
    {
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, """{"theme":"custom","compaction":{"reserveTokens":16384,"modelOverrides":{"ollama/qwen3:0.6b":{"reserveTokens":512},"other/model":{"keepRecentTokens":8000}}}}""");
        Assert.IsNull(await importer.ImportAsync(Local, [Model], 4096));
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.AreEqual("custom", root["theme"]!.GetValue<string>());
        Assert.AreEqual(16384, root["compaction"]!["reserveTokens"]!.GetValue<int>());
        var overrides = root["compaction"]!["modelOverrides"]!;
        Assert.AreEqual(512, overrides["ollama/qwen3:0.6b"]!["reserveTokens"]!.GetValue<int>());
        Assert.AreEqual(2048, overrides["ollama/qwen3:0.6b"]!["keepRecentTokens"]!.GetValue<int>());
        Assert.AreEqual(8000, overrides["other/model"]!["keepRecentTokens"]!.GetValue<int>());
    }

    [TestMethod]
    public async Task SettingsFailureReportsCommittedModelsAndDoesNotOverwriteSettings()
    {
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, "broken settings");
        var warning = await importer.ImportAsync(Local, [Model], 4096);
        Assert.IsNotNull(warning);
        Assert.IsTrue((await importer.ReadAsync()).Single().ModelIds.Contains(Model.Id));
        Assert.AreEqual("broken settings", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task ProviderFiltersDoNotAffectOnboardingInventory()
    {
        var service = new FakeProviderService { Providers = [new("test", "Test provider", true, "stored", "api_key", true, false, true, "", [new("model", "Model name", 4096, 1024, false, [])])] };
        var vm = new ProvidersViewModel(service);
        await vm.RefreshAsync();
        vm.Search = "no match";
        Assert.IsTrue(vm.NoResults);
        Assert.IsTrue(vm.HasConfiguredProvider);
        Assert.AreEqual(1, vm.Cards.Count);
        vm.Search = "model name";
        vm.ConfiguredOnly = true;
        Assert.AreEqual(1, vm.VisibleCards.Count);
    }

    [TestMethod]
    public async Task OllamaCardIsAvailableEvenWhenProviderRuntimeFails()
    {
        using var handler = new FakeOllamaHandler(_ => new(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        var vm = new ProvidersViewModel(new FakeProviderService { Fail = true }, new(new(http), importer, () => { }));
        await vm.RefreshAsync();
        Assert.IsTrue(vm.VisibleCards.Single().IsOllama);
        Assert.IsFalse(vm.HasConfiguredProvider);
        Assert.IsFalse(vm.HasInventory);
    }

    private static HttpResponseMessage Reply(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
}
