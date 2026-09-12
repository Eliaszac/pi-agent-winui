using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.ViewModels.Providers;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ModelPickerTests
{
    private string directory = null!;
    private ModelFavoritesStore store = null!;
    private static readonly PiModel First = new("openai", "shared", "Same name");
    private static readonly PiModel Second = new("ollama", "shared", "Same name");

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "PiModelPickerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        store = new(Path.Combine(directory, "favorites.json"));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, true);

    [TestMethod]
    public async Task FavoritesPersistByProviderAndNeverChangeSelectedModel()
    {
        var picker = new ModelPickerViewModel(store);
        picker.Update([First, Second], First);
        await picker.OpenAsync();
        Assert.AreEqual("openai", picker.SelectedProvider?.Id);
        picker.Search = "same name";
        await picker.ToggleFavoriteAsync(picker.Items.Single(item => item.Model == Second));
        Assert.IsTrue(picker.Items.Single(item => item.Model == First).IsSelected);
        Assert.IsFalse(picker.Items.Single(item => item.Model == Second).IsSelected);
        var reopened = new ModelPickerViewModel(new(Path.Combine(directory, "favorites.json")));
        reopened.Update([First, Second], First);
        await reopened.OpenAsync();
        Assert.AreEqual("", reopened.SelectedProvider?.Id);
        Assert.AreEqual(Second, reopened.Items.Single().Model);
    }

    [TestMethod]
    public async Task RemovedModelsKeepFavoritesAndReturnWhenAvailableAgain()
    {
        await store.SetAsync(ModelIdentity.From(Second), true);
        var picker = new ModelPickerViewModel(store);
        picker.Update([First], First);
        await picker.OpenAsync();
        Assert.AreEqual("openai", picker.SelectedProvider?.Id);
        picker.Update([Second, First, Second], First);
        await picker.OpenAsync();
        Assert.AreEqual(1, picker.Items.Count);
        Assert.AreEqual(Second, picker.Items[0].Model);
    }

    [TestMethod]
    public async Task SearchFindsIdsAndProvidersAcrossCurrentFilter()
    {
        var picker = new ModelPickerViewModel(store);
        picker.Update([First, Second], First);
        await picker.OpenAsync();
        picker.Search = " OLLAMA ";
        Assert.AreEqual(Second, picker.Items.Single().Model);
        picker.Search = "shared";
        Assert.AreEqual(2, picker.Items.Count);
        picker.SelectedProvider = picker.Providers.Single(item => item.Id == "openai");
        // Already selected: explicitly clearing search returns to its provider.
        picker.Search = "";
        Assert.AreEqual(First, picker.Items.Single().Model);
        picker.Search = "no such model";
        Assert.IsTrue(picker.IsEmpty);
    }

    [TestMethod]
    public async Task RemovingLastFavoriteShowsEmptyState()
    {
        await store.SetAsync(ModelIdentity.From(First), true);
        var picker = new ModelPickerViewModel(store);
        picker.Update([First], First);
        await picker.OpenAsync();
        await picker.ToggleFavoriteAsync(picker.Items.Single());
        Assert.IsTrue(picker.IsEmpty);
        Assert.AreEqual("", picker.SelectedProvider?.Id);
        Assert.AreEqual(0, (await store.ReadAsync()).Count);
    }

    [TestMethod]
    public async Task SeparateStoreInstancesMergeConcurrentFavorites()
    {
        var other = new ModelFavoritesStore(Path.Combine(directory, "favorites.json"));
        await Task.WhenAll(store.SetAsync(ModelIdentity.From(First), true), other.SetAsync(ModelIdentity.From(Second), true));
        Assert.AreEqual(2, (await store.ReadAsync()).Count);
    }

    [TestMethod]
    public async Task InvalidFavoritesStayIntactAndDoNotBlockModelBrowsing()
    {
        var path = Path.Combine(directory, "favorites.json");
        await File.WriteAllTextAsync(path, "broken json");
        var picker = new ModelPickerViewModel(store);
        picker.Update([First], First);
        await picker.OpenAsync();
        Assert.AreEqual(First, picker.Items.Single().Model);
        Assert.IsFalse(string.IsNullOrEmpty(picker.Error));
        await picker.ToggleFavoriteAsync(picker.Items.Single());
        Assert.IsFalse(picker.Items.Single().IsFavorite);
        Assert.AreEqual("broken json", await File.ReadAllTextAsync(path));
    }
}
