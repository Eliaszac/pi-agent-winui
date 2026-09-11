using System.Collections.ObjectModel;

namespace PiAgentGui.Utilities;

/// <summary>Updates grouped lists without resetting surviving rows and their editing/focus state.</summary>
public static class ObservableCollectionSynchronizer
{
    public static void Synchronize<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        var wanted = desired.ToHashSet();
        var existingItems = target.ToHashSet();
        for (var index = target.Count - 1; index >= 0; index--)
            if (!wanted.Contains(target[index])) target.RemoveAt(index);
        for (var index = 0; index < desired.Count; index++)
        {
            if (index < target.Count && EqualityComparer<T>.Default.Equals(target[index], desired[index])) continue;
            var existing = existingItems.Contains(desired[index]) ? target.IndexOf(desired[index]) : -1;
            if (existing >= 0) target.Move(existing, index);
            else target.Insert(index, desired[index]);
        }
    }
}
