using System.Text.Json;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Providers;

public sealed class ProvidersViewModel(IProviderService service) : ObservableObject
{
    private IReadOnlyList<ProviderCardViewModel> cards = [];
    private bool busy;
    private string error = "";
    public IReadOnlyList<ProviderCardViewModel> Cards { get => cards; private set => SetProperty(ref cards, value); }
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(CanInteract)); } }
    public bool CanInteract => !IsBusy;
    public string Error { get => error; private set => SetProperty(ref error, value); }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        try { await RunAsync("list"); }
        catch (Exception exception) { Error = exception is OperationCanceledException ? "Refresh cancelled. Try again." : exception.Message; }
    }

    public async Task RunAsync(string action, ProviderCardViewModel? card = null, string? method = null,
        Func<JsonElement, CancellationToken, Task<string?>>? prompt = null, Action<JsonElement>? notify = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy) throw new InvalidOperationException("Wait for the current provider operation to finish.");
        IsBusy = true;
        Error = "";
        try
        {
            var result = await Task.Run(() => service.RunAsync(action, card?.Id, method, prompt, notify, cancellationToken), cancellationToken);
            Cards = result.OrderByDescending(provider => provider.Configured).ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .Select(provider => new ProviderCardViewModel(provider)).ToArray();
        }
        finally { IsBusy = false; }
    }
}
