using System.Text.Json;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

internal sealed class FakeProviderService : IProviderService
{
    public IReadOnlyList<PiProvider> Providers { get; set; } = [];
    public bool Fail { get; set; }
    public event Action? CredentialsChanged { add { } remove { } }
    public Task<IReadOnlyList<PiProvider>> RunAsync(string action, string? provider = null, string? method = null,
        Func<JsonElement, CancellationToken, Task<string?>>? prompt = null, Action<JsonElement>? notify = null, CancellationToken cancellationToken = default)
        => Fail ? Task.FromException<IReadOnlyList<PiProvider>>(new IOException("Unavailable")) : Task.FromResult(Providers);
}
