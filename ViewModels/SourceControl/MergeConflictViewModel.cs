using PiAgentGui.Models.SourceControl;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.SourceControl;

public sealed class MergeConflictViewModel : ObservableObject
{
    private string result;
    private bool delete;
    private bool reviewed;
    private int selected;
    private readonly Stack<(string Text, bool Delete, bool Reviewed)> undo = [];
    public MergeConflict Snapshot { get; }
    public MergeConflictViewModel(MergeConflict snapshot) { Snapshot = snapshot; result = snapshot.WorkingText; }
    public string Result
    {
        get => result;
        set { if (SetProperty(ref result, value)) { delete = false; reviewed = true; Notify(); } }
    }
    public IReadOnlyList<ConflictBlock> Blocks => ConflictMarkers.Parse(result);
    public ConflictBlock? Selected => Blocks.Count == 0 ? null : Blocks[Math.Clamp(selected, 0, Blocks.Count - 1)];
    public string Status => delete ? "Result: delete file" : Blocks.Count > 0 ? $"Conflict {Math.Clamp(selected, 0, Blocks.Count - 1) + 1} of {Blocks.Count} unresolved" : ConflictMarkers.HasMarkers(result) ? "Incomplete conflict markers remain" : reviewed ? "Ready to apply · review the result" : "Review the file, then mark it resolved";
    public bool CanApply => reviewed && (delete || !ConflictMarkers.HasMarkers(result));
    public bool HasBlocks => Blocks.Count > 0;
    public bool HasSelectedBase => Selected?.Base is not null;
    public bool CanUndo => undo.Count > 0;
    public bool Delete => delete;
    public string LeftLabel => Snapshot.Left is null ? "Ours · deleted" : "Ours · index stage 2";
    public string RightLabel => Snapshot.Right is null ? "Theirs · deleted" : "Theirs · index stage 3";
    public void Navigate(int delta) { var count = Blocks.Count; selected = count == 0 ? 0 : (selected + delta + count) % count; Notify(); }
    public void Accept(string side, bool wholeFile = false)
    {
        var block = Selected;
        if (!wholeFile && block is null) return;
        undo.Push((result, delete, reviewed));
        if (wholeFile)
        {
            var source = side == "left" ? Snapshot.Left : Snapshot.Right;
            result = NormalizeNewlines(source ?? ""); delete = source is null;
        }
        else
        {
            var replacement = side switch { "left" => block!.Left, "right" => block!.Right, "both" => block!.Left + block.Right, _ => block!.Base ?? "" };
            result = result.Remove(block!.Start, block.Length).Insert(block.Start, replacement); delete = false;
        }
        reviewed = true; Notify();
    }
    public void MarkReviewed() { reviewed = true; Notify(); }
    public void SynchronizeEditorText(string text) { result = text; Notify(); }
    public void Undo()
    {
        if (undo.TryPop(out var previous)) { result = previous.Text; delete = previous.Delete; reviewed = previous.Reviewed; Notify(); }
    }
    public string TextToSave => NormalizeNewlines(result);
    private string NormalizeNewlines(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Snapshot.WorkingText.Contains("\r\n") ? "\r\n" : "\n");
    private void Notify()
    {
        selected = Math.Clamp(selected, 0, Math.Max(0, Blocks.Count - 1));
        foreach (var name in new[] { nameof(Result), nameof(Status), nameof(CanApply), nameof(HasBlocks), nameof(HasSelectedBase), nameof(CanUndo), nameof(Delete) }) OnPropertyChanged(name);
    }
}
