namespace PiAgentGui.Utilities;

/// <summary>Bounds prompt markers without discarding older navigation targets.</summary>
public sealed class PromptNavigationPages
{
    public const int PageSize = 50;
    public int Count { get; private set; }
    public int Page { get; private set; }
    public int PageCount => Math.Max(1, (Count + PageSize - 1) / PageSize);
    public int Start => Page * PageSize;
    public int VisibleCount => Math.Min(PageSize, Math.Max(0, Count - Start));
    public bool HasPrevious => Page > 0;
    public bool HasNext => Page + 1 < PageCount;
    public void Update(int count)
    {
        var wasLatest = !HasNext;
        Count = Math.Max(0, count);
        Page = wasLatest ? PageCount - 1 : Math.Min(Page, PageCount - 1);
    }
    public void Previous() { if (HasPrevious) Page--; }
    public void Next() { if (HasNext) Page++; }
}
