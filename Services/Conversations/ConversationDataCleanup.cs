using System.Text.Json;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Records deletion intent and retries unavailable target cleanup on the next catalog load.</summary>
public sealed class ConversationDataCleanup(PiSessionPaths paths, IProjectRepository projects, Func<ExecutionTarget, string, Task<int>> forget)
{
    public async Task ScheduleAsync(Guid project, Guid conversation, ExecutionTarget target)
    {
        var session = paths.GetSessionFile(project, conversation);
        if (WorkspaceActivityLease.PendingRecovery(session) is not null)
            throw new IOException("Inspect this conversation's pending checkpoint recovery before deleting it.");
        Directory.CreateDirectory(paths.CleanupDirectory);
        var destination = Path.Combine(paths.CleanupDirectory, project.ToString("N") + conversation.ToString("N") + ".json");
        var temporary = destination + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new ConversationCleanupRequest(project, conversation, target)));
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<string?> RunPendingAsync()
    {
        if (!Directory.Exists(paths.CleanupDirectory)) return null;
        var catalog = await projects.GetAllAsync();
        var pending = false;
        foreach (var file in Directory.EnumerateFiles(paths.CleanupDirectory, "*.json"))
        {
            try
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked cleanup records are unsupported.");
                var request = JsonSerializer.Deserialize<ConversationCleanupRequest>(await File.ReadAllTextAsync(file)) ?? throw new IOException("Invalid cleanup record.");
                if (catalog.Any(p => p.Id == request.ProjectId && p.Conversations.Any(c => c.Id == request.ConversationId))) continue;
                var session = paths.GetSessionFile(request.ProjectId, request.ConversationId);
                using (WorkspaceActivityLease.Acquire(true))
                {
                    if (WorkspaceActivityLease.PendingRecovery(session) is not null)
                        throw new IOException("Inspect pending checkpoint recovery before deleting its session data.");
                    DeleteSessionFile(session);
                    DeleteSessionFile(session + ".settings.json");
                }
                await forget(request.Target, session);
                File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                pending = true;
            }
        }
        return pending ? "Conversation data cleanup is pending. Close active runs and terminals, resolve pending restores, and reconnect unavailable targets. Cleanup retries when the project list reloads." : null;
    }

    private static void DeleteSessionFile(string file)
    {
        for (var parent = Path.GetDirectoryName(file); parent is not null; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked session storage cannot be deleted.");
        if (!File.Exists(file)) return;
        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked session files cannot be deleted.");
        File.Delete(file);
    }
}
