using PiAgentGui.Models.Pi;

namespace PiAgentGui.Utilities;

public static class AgentProcessTree
{
    public static IReadOnlyList<AgentProcess> Select(ProcessIdentity root, IReadOnlyList<AgentProcess> snapshot, IReadOnlySet<ProcessIdentity> known)
    {
        var owners = new Dictionary<int, ProcessIdentity> { [root.Id] = root };
        var selected = new Dictionary<int, AgentProcess>();
        foreach (var item in snapshot.Where(item => item.Identity != root && known.Contains(item.Identity)))
        {
            owners[item.Identity.Id] = item.Identity;
            selected[item.Identity.Id] = item;
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var item in snapshot)
            {
                if (item.Identity.Id == root.Id || selected.ContainsKey(item.Identity.Id)) continue;
                if (!owners.TryGetValue(item.ParentId, out var parent) || item.Identity.StartedUtc < parent.StartedUtc) continue;
                owners[item.Identity.Id] = item.Identity;
                selected[item.Identity.Id] = item;
                changed = true;
            }
        } while (changed);
        return selected.Values.OrderBy(item => item.Identity.StartedUtc).ThenBy(item => item.Identity.Id).ToArray();
    }
}
