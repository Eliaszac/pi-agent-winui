using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Services.Conversations;

public interface IResearchRunner
{
    Task<string> RunAsync(ResearchTask task, CancellationToken cancellationToken);
}
