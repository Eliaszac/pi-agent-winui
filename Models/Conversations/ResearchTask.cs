namespace PiAgentGui.Models.Conversations;

public sealed record ResearchTask(Guid Id, Guid ConversationId, string Directory, string Title, string Question,
    string Provider, string Model, string Effort, string Status, string Result, DateTimeOffset CreatedAt);
