using System.Text.Json;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

public interface IProviderService
{
    event Action? CredentialsChanged;
    Task<IReadOnlyList<PiProvider>> RunAsync(string action, string? provider = null, string? method = null,
        Func<JsonElement, CancellationToken, Task<string?>>? prompt = null, Action<JsonElement>? notify = null,
        CancellationToken cancellationToken = default);
}
