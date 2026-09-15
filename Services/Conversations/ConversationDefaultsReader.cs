using System.Text.Json;
using PiAgentGui.Models.Home;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Home;

namespace PiAgentGui.Services.Conversations;

/// <summary>Reuses bounded local usage reads; unavailable history must not prevent a new conversation.</summary>
public sealed class ConversationDefaultsReader(IProjectRepository projects, SessionUsageReader usage)
{
    public async Task<IReadOnlyList<UsageSample>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await projects.GetAllAsync(cancellationToken).ConfigureAwait(false);
            return (await usage.ReadAsync(catalog, cancellationToken).ConfigureAwait(false)).Samples;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }
}
