using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class MergeConflictTests
{
    [DataTestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("\r")]
    public void ChoicesKeepNonConflictingEditsAndUndoRestoresConflict(string newline)
    {
        var text = "before\n<<<<<<< ours\nleft\n=======\nright\n>>>>>>> theirs\nafter\n".Replace("\n", newline);
        var model = new MergeConflictViewModel(Snapshot(text));
        Assert.AreEqual(1, model.Blocks.Count); Assert.IsFalse(model.CanApply);
        model.Accept("both");
        Assert.AreEqual("before\nleft\nright\nafter\n".Replace("\n", newline), model.Result);
        Assert.IsTrue(model.CanApply);
        model.Undo(); Assert.AreEqual(text, model.Result); Assert.IsFalse(model.CanApply);
    }

    [TestMethod]
    public void Diff3BaseAndMultipleConflictsAreIndependent()
    {
        var block = "<<<<<<< ours\nleft\n||||||| base\noriginal\n=======\nright\n>>>>>>> theirs\n";
        var model = new MergeConflictViewModel(Snapshot(block + "unchanged\n" + block));
        model.Navigate(1); model.Accept("right");
        Assert.AreEqual(block + "unchanged\nright\n", model.Result);
        Assert.IsTrue(model.HasSelectedBase);
        model.Accept("base");
        Assert.AreEqual("original\nunchanged\nright\n", model.Result);
        Assert.IsTrue(model.CanApply);
    }

    [TestMethod]
    public void CustomWidthMarkersAndIncompleteMarkersAreHandled()
    {
        var text = "<<<<<<<<<< ours\nleft\n==========\nright\n>>>>>>>>>> theirs";
        Assert.AreEqual(1, ConflictMarkers.Parse(text).Count);
        var model = new MergeConflictViewModel(Snapshot("<<<<<<< ours\nleft"));
        model.MarkReviewed(); Assert.IsFalse(model.CanApply);
        model.Result = "manually resolved\n"; Assert.IsTrue(model.CanApply);
    }

    [TestMethod]
    public void DeletedSideIsExplicitAndUndoable()
    {
        var model = new MergeConflictViewModel(Snapshot("working") with { Left = null });
        Assert.IsFalse(model.CanApply);
        model.Accept("left", true); Assert.IsTrue(model.Delete); Assert.IsTrue(model.CanApply);
        model.Undo(); Assert.IsFalse(model.Delete); Assert.AreEqual("working", model.Result);
        model.Accept("right", true); Assert.IsFalse(model.Delete); Assert.AreEqual("right", model.Result);
    }

    private static MergeConflict Snapshot(string text) => new("root", "file", "head", "index", "base", "left", "right", null, text, false);
}
