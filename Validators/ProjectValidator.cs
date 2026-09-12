using System.Text.Json;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Validators;

/// <summary>Checks project records at the persistence boundary.</summary>
internal static class ProjectValidator
{
    public static void Validate(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Id == Guid.Empty)
            throw new ArgumentException("A project must have an identifier.", nameof(project));

        ArgumentException.ThrowIfNullOrWhiteSpace(project.Name);
        ArgumentNullException.ThrowIfNull(project.Targets);
        if (project.Targets.Count == 0) _ = ProjectPath.Normalize(project.Path);
        if (project.Targets.Count > 32) throw new ArgumentException("A project supports up to 32 targets.");
        var targets = ProjectTargets.All(project);
        if (targets.Select(target => target.Id).Distinct().Count() != targets.Count ||
            targets.Select(ProjectTargets.Identity).Distinct(StringComparer.Ordinal).Count() != targets.Count)
            throw new ArgumentException("The project contains duplicate targets.");
        foreach (var target in targets) _ = ProjectTargets.Normalize(target);
        _ = ProjectTargets.Resolve(project);
        ValidateMetadata(project.Metadata);
        ArgumentNullException.ThrowIfNull(project.Conversations);
        var identifiers = new HashSet<Guid>();
        foreach (var conversation in project.Conversations)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (conversation.Id == Guid.Empty || !identifiers.Add(conversation.Id) || conversation.CreatedAt == default)
                throw new ArgumentException("The conversation entry is invalid.", nameof(project));
            ArgumentException.ThrowIfNullOrWhiteSpace(conversation.Title);
            _ = ProjectTargets.Resolve(project, conversation.TargetId ?? project.Id);
        }
    }

    public static void ValidateMetadata(JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Project metadata must be a JSON object.", nameof(metadata));
    }
}
