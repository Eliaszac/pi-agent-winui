using PiAgentGui.Models.Projects;
using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>Resolves legacy local catalogs and validates explicit target identities without contacting hosts.</summary>
public static class ProjectTargets
{
    public static IReadOnlyList<ExecutionTarget> All(Project project) => project.Targets.Count > 0 ? project.Targets :
        [new() { Id = project.Id, Name = "This computer", Path = project.Path }];

    public static ExecutionTarget Resolve(Project project, Guid? targetId = null) =>
        All(project).FirstOrDefault(target => target.Id == (targetId ?? project.DefaultTargetId ?? project.Id))
        ?? throw new ArgumentException("The execution target is no longer available. Reload the project list.");

    public static ExecutionTarget Normalize(ExecutionTarget target)
    {
        if (target.Id == Guid.Empty || string.IsNullOrWhiteSpace(target.Name) || target.Name.Length > 120)
            throw new ArgumentException("Enter a target name of up to 120 characters.");
        if (target.Kind is not ("local" or "wsl" or "ssh")) throw new ArgumentException("Choose Local, WSL, or SSH.");
        if (target.SshAuthentication is not ("key" or "password")) throw new ArgumentException("Choose SSH key or password authentication.");
        if (target.Kind == "ssh" && target.SshAuthentication == "password" && !target.HasSshSecret)
            throw new ArgumentException("Enter the SSH password.");
        if (target.SshKeyPath.Length > 0 && (!Path.IsPathFullyQualified(target.SshKeyPath) || target.SshKeyPath.Any(char.IsControl)))
            throw new ArgumentException("Enter the full local path to the SSH private key.");
        var path = target.Path.Trim();
        var host = target.Host.Trim();
        if (target.IsLocal) { path = ProjectPath.Normalize(path); host = ""; }
        else
        {
            if (!path.StartsWith('/') || path.Contains('\\') || path.Any(char.IsControl) || path.Split('/').Any(part => part is "." or ".."))
                throw new ArgumentException("Enter an absolute Linux folder, such as /home/user/project, without dot segments.");
            path = path.TrimEnd('/');
            if (path.Length == 0) throw new ArgumentException("Choose a workspace folder below the filesystem root.");
            if (host.Length > 200 || !Regex.IsMatch(host, target.Kind == "ssh" ? @"\A[A-Za-z0-9_][A-Za-z0-9_.@-]*\z" : @"\A[A-Za-z0-9_][A-Za-z0-9_. -]*\z"))
                throw new ArgumentException(target.Kind == "ssh" ? "Enter an SSH config alias or user@hostname. Configure ports and keys in SSH config." : "Enter the installed WSL distribution name.");
        }
        return target with { Name = target.Name.Trim(), Path = path, Host = host };
    }

    public static string Identity(ExecutionTarget target) => target.IsLocal ? "local:" + ProjectPath.Normalize(target.Path).ToUpperInvariant()
        : target.Kind + ":" + target.Host + ":" + target.Path;
}
