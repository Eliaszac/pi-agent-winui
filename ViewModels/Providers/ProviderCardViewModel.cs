using PiAgentGui.Models.Pi;

namespace PiAgentGui.ViewModels.Providers;

public sealed class ProviderCardViewModel(PiProvider provider, bool isOllama = false, string? status = null, string? source = null)
{
    public bool IsOllama => isOllama;
    public bool ShowDetails => !IsOllama;
    internal PiProvider Provider => provider;
    public string Name => provider.Name;
    public string Id => provider.Id;
    public string Status => status ?? (provider.Configured ? "Configured" : "Set up required");
    public string Source => source ?? (provider.Configured
        ? $"{(provider.Method == "oauth" ? "Sign-in" : provider.Method == "api_key" ? "API key" : "Credentials")} · {(provider.Source == "stored" ? "Saved in Pi" : provider.Source)}"
        : "Available across all projects once configured.");
    public string ModelCount => $"{provider.Models.Count} {(IsOllama ? "imported models" : "models")}";
    public string SetupLabel => isOllama ? "Manage connection" : provider.Configured ? "Manage" : "Set up";
    public bool CanSignIn => provider.Oauth;
    public bool CanAddKey => provider.ApiKey;
    public bool CanSignOut => provider.Stored;
    public string LoginLabel => provider.LoginLabel;
    public string KeyLabel => provider.Stored ? "Replace API key" : "Add API key";
    public string ModelDetails => provider.Models.Count == 0 ? "No models listed by Pi."
        : string.Join("\n\n", provider.Models.Select(model => $"{model.Name}\n{model.Id}\nContext: {model.ContextWindow:N0} · Output: {model.MaxTokens:N0}" +
            (model.Reasoning ? " · Reasoning" : "") + (model.Input.Contains("image") ? " · Images" : "")));
}
