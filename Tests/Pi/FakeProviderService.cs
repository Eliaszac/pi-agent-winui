using System.Text.Json;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;

namespace PiAgentGui.Tests.Pi;

internal sealed class FakeProviderService : IProviderService
{
    public IReadOnlyList<PiProvider> Providers { get; set; } = [];
    public bool Fail { get; set; }
    public Task? ReadBarrier { get; set; }
    public event Action? CredentialsChanged { add { } remove { } }
    public async Task<IReadOnlyList<PiProvider>> RunAsync(string action, string? provider = null, string? method = null,
        Func<JsonElement, CancellationToken, Task<string?>>? prompt = null, Action<JsonElement>? notify = null, CancellationToken cancellationToken = default)
    {
        if (ReadBarrier is not null) await ReadBarrier.WaitAsync(cancellationToken);
        if (Fail) throw new IOException("Unavailable");
        return Providers;
    }
}
