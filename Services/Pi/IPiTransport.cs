using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>A replaceable, owned JSONL connection to a single Pi process.</summary>
public interface IPiTransport : IAsyncDisposable
{
    Task StartAsync(PiLaunchRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken = default);
    Task WriteLineAsync(string line, CancellationToken cancellationToken = default);
}
