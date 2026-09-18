using System.Text.Json;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;

namespace PiAgentGui.Tests.Projects;

internal sealed class InMemoryProjectRepository(params Project[] projects) : IProjectRepository
{
    public Task AddTargetAsync(Guid projectId, ExecutionTarget target, bool makeDefault, CancellationToken cancellationToken = default)
    {
        var i = Projects.FindIndex(p => p.Id == projectId);
        Projects[i] = Projects[i] with { Targets = [.. Utilities.ProjectTargets.All(Projects[i]), target], DefaultTargetId = makeDefault ? target.Id : Projects[i].DefaultTargetId ?? projectId };
        return Task.CompletedTask;
    }
    public Task SetDefaultTargetAsync(Guid projectId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var i = Projects.FindIndex(p => p.Id == projectId);
        Projects[i] = Projects[i] with { DefaultTargetId = targetId };
        return Task.CompletedTask;
    }
    public Task? ScriptWriteBarrier { get; set; }
    public async Task UpdateScriptsAsync(Guid projectId, ProjectScriptSettings settings, CancellationToken cancellationToken = default, Guid? targetId = null)
    {
        if (ScriptWriteBarrier is not null) await ScriptWriteBarrier;
        var index = Projects.FindIndex(item => item.Id == projectId);
        Projects[index] = Projects[index] with { Metadata = Utilities.ProjectScripts.Write(Projects[index].Metadata, settings, targetId) };
    }
    public Task TouchConversationAsync(Guid projectId, Guid conversationId, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        var index = Projects.FindIndex(project => project.Id == projectId);
        Projects[index] = Projects[index] with { Conversations = Projects[index].Conversations.Select(conversation => conversation.Id == conversationId
            ? conversation with { LastUsedAt = usedAt } : conversation).ToArray() };
        return Task.CompletedTask;
    }
    public Exception? CopyRegistrationError { get; set; }
    public Task AddConversationCopyAsync(Guid projectId, Guid sourceId, ConversationDraft conversation, CancellationToken cancellationToken = default)
    {
        if (CopyRegistrationError is not null) return Task.FromException(CopyRegistrationError);
        var index = Projects.FindIndex(item => item.Id == projectId);
        if (index < 0 || !Projects[index].Conversations.Any(item => item.Id == sourceId)) throw new KeyNotFoundException();
        Projects[index] = Projects[index] with { Conversations = [.. Projects[index].Conversations, conversation] };
        return Task.CompletedTask;
    }
    public List<Project> Projects { get; } = [.. projects];
    public Exception? ReadError { get; set; }
    public Task? ReadBarrier { get; set; }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (ReadBarrier is not null) await ReadBarrier;
        if (ReadError is not null) throw ReadError;
        return Projects.ToArray();
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        Projects.Add(project);
        return Task.CompletedTask;
    }

    public Task UpdateMetadataAsync(Guid projectId, JsonElement metadata, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ConversationDraft> AddConversationAsync(Guid projectId, CancellationToken cancellationToken = default, Guid? targetId = null)
    {
        var index = Projects.FindIndex(project => project.Id == projectId);
        var draft = new ConversationDraft { Id = Guid.NewGuid(), TargetId = Utilities.ProjectTargets.Resolve(Projects[index], targetId).Id, Title = "Conversation 1", CreatedAt = DateTimeOffset.UtcNow, IsTitleManual = false };
        Projects[index] = Projects[index] with { Conversations = [.. Projects[index].Conversations, draft] };
        return Task.FromResult(draft);
    }

    public Task RenameProjectAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var index = Projects.FindIndex(project => project.Id == projectId);
        Projects[index] = Projects[index] with { Name = name.Trim() };
        return Task.CompletedTask;
    }
    public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        Projects.RemoveAll(project => project.Id == projectId);
        return Task.CompletedTask;
    }
    public Task RenameConversationAsync(Guid projectId, Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return ChangeConversation(projectId, conversationId, conversation => conversation with { Title = title.Trim(), IsTitleManual = true });
    }
    public async Task<bool> SetGeneratedTitleAsync(Guid projectId, Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        var applied = false;
        await ChangeConversation(projectId, conversationId, conversation =>
        {
            if (conversation.IsTitleManual) return conversation;
            applied = true;
            return conversation with { Title = title.Trim() };
        });
        return applied;
    }
    public Task SetConversationSettledAsync(Guid projectId, Guid conversationId, bool settled, CancellationToken cancellationToken = default) =>
        ChangeConversation(projectId, conversationId, conversation => conversation with { IsSettled = settled });
    public Task DeleteConversationAsync(Guid projectId, Guid conversationId, CancellationToken cancellationToken = default) =>
        ChangeConversation(projectId, conversationId, _ => null);
    private Task ChangeConversation(Guid projectId, Guid conversationId, Func<ConversationDraft, ConversationDraft?> change)
    {
        var index = Projects.FindIndex(project => project.Id == projectId);
        var conversations = Projects[index].Conversations.ToList();
        var conversationIndex = conversations.FindIndex(conversation => conversation.Id == conversationId);
        var updated = change(conversations[conversationIndex]);
        if (updated is null) conversations.RemoveAt(conversationIndex);
        else conversations[conversationIndex] = updated;
        Projects[index] = Projects[index] with { Conversations = conversations };
        return Task.CompletedTask;
    }
}
