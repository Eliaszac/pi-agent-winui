using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Revalidates conversation ownership before stopping a subprocess and its observed descendants.</summary>
public sealed class AgentProcessStopper(
    Func<ProcessIdentity, IReadOnlySet<ProcessIdentity>, IReadOnlyList<AgentProcess>> read,
    Action<ProcessIdentity> terminate)
{
    public void Stop(ProcessIdentity root, ProcessIdentity target, IReadOnlySet<ProcessIdentity> known)
    {
        if (target.Id == root.Id || !known.Contains(target)) throw new InvalidOperationException("This process does not belong to the selected conversation.");
        var current = read(root, known);
        if (!current.Any(item => item.Identity == target)) return;
        var children = AgentProcessTree.Select(target, current, new HashSet<ProcessIdentity>());
        var failed = false;
        foreach (var identity in children.Select(item => item.Identity).Reverse().Append(target))
        {
            try { terminate(identity); }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { failed = true; }
        }
        if (failed) throw new IOException("Some processes couldn't be stopped. They may require additional permissions.");
    }
}
