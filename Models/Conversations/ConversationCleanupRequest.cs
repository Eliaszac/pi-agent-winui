using PiAgentGui.Models.Projects;

namespace PiAgentGui.Models.Conversations;

public sealed record ConversationCleanupRequest(Guid ProjectId, Guid ConversationId, ExecutionTarget Target);
