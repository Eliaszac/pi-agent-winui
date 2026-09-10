using System.Collections.ObjectModel;

namespace PiAgentGui.Utilities;

/// <summary>Updates grouped lists without resetting surviving rows and their editing/focus state.</summary>
public static class ObservableCollectionSynchronizer
{
    public static void Synchronize<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        for (var index = target.Count - 1; index >= 0; index--)
            if (!desired.Contains(target[index])) target.RemoveAt(index);
        for (var index = 0; index < desired.Count; index++)
        {
            if (index < target.Count && EqualityComparer<T>.Default.Equals(target[index], desired[index])) continue;
            var existing = target.IndexOf(desired[index]);
            if (existing >= 0) target.Move(existing, index);
            else target.Insert(index, desired[index]);
        }
    }
}
