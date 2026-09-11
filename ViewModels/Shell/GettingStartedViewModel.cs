using System.ComponentModel;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Extensions;
using PiAgentGui.ViewModels.Providers;

namespace PiAgentGui.ViewModels.Shell;

/// <summary>Separates missing provider configuration, unknown inventory and optional recommendations.</summary>
public sealed class GettingStartedViewModel : ObservableObject, IDisposable
{
    private readonly ProvidersViewModel providers;
    private readonly ExtensionsViewModel extensions;
    private readonly GlobalConfigurationFile preferences;
    private bool initialized;
    private bool extensionsChecked;
    private bool dismissed;
    public string Error { get; private set; } = "";
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool NeedsProvider => providers.HasInventory && !providers.HasConfiguredProvider;
    public bool ShowProviderSetup => initialized && (!providers.HasInventory || NeedsProvider);
    public bool ShowRecommendations => initialized && providers.HasInventory && providers.HasConfiguredProvider && extensionsChecked && !dismissed && Missing.Count > 0;
    public bool IsVisible => ShowProviderSetup || ShowRecommendations;
    public string ProviderTitle => NeedsProvider ? "Get started" : "Provider setup";
    public string ProviderMessage => NeedsProvider ? "Connect a provider to start chatting." : providers.IsBusy ? "Checking provider configuration…" : "Couldn't check provider configuration. Open Providers to retry.";
    public string ProviderAction => NeedsProvider ? "Connect provider" : "Open Providers";
    public IReadOnlyList<ExtensionCardViewModel> Missing => extensions.Cards.Where(card => card.NeedsSetup).ToArray();
    public string ExtensionTitle => $"Optional extensions · {extensions.Cards.Count - Missing.Count}/{extensions.Cards.Count} ready";
    public AsyncRelayCommand DismissCommand { get; }

    public GettingStartedViewModel(ProvidersViewModel providers, ExtensionsViewModel extensions, string preferencePath)
    {
        this.providers = providers; this.extensions = extensions; preferences = new(preferencePath);
        providers.PropertyChanged += OnChanged;
        foreach (var card in extensions.Cards) card.PropertyChanged += OnChanged;
        DismissCommand = new(async _ =>
        {
            await preferences.UpdateAsync(root => root["extensionSuggestionsDismissed"] = true);
            dismissed = true; Notify();
        }, _ => ReportError("Couldn't save your preference. Try dismissing again."));
    }
    public void ReportError(string message) { Error = message; OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(HasError)); }
    public async Task InitializeAsync()
    {
        if (initialized) return;
        try { dismissed = (await preferences.ReadAsync())["extensionSuggestionsDismissed"]?.GetValue<bool>() == true; }
        catch (Exception) { Error = "Couldn't read the getting-started preference."; }
        initialized = true;
        await Task.WhenAll(providers.RefreshAsync(), extensions.RefreshCommand.ExecuteAsync());
        extensionsChecked = true; Notify();
    }
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => Notify();
    private void Notify()
    {
        foreach (var property in new[] { nameof(NeedsProvider), nameof(ShowProviderSetup), nameof(ShowRecommendations), nameof(IsVisible),
            nameof(ProviderTitle), nameof(ProviderMessage), nameof(ProviderAction), nameof(Missing), nameof(ExtensionTitle), nameof(Error), nameof(HasError) }) OnPropertyChanged(property);
    }
    public void Dispose()
    {
        providers.PropertyChanged -= OnChanged;
        foreach (var card in extensions.Cards) card.PropertyChanged -= OnChanged;
    }
}
