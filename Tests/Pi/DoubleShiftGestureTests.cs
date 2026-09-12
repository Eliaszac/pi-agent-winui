using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class DoubleShiftGestureTests
{
    [TestMethod]
    public void TwoTapsOpenButTypingAndHeldShiftDoNot()
    {
        var gesture = new DoubleShiftGesture();
        gesture.Down(true, false, 0); Assert.IsFalse(gesture.Up(true, 50));
        gesture.Down(true, false, 150); Assert.IsTrue(gesture.Up(true, 200));
        gesture.Down(true, false, 300); gesture.Down(false, false, 310);
        Assert.IsFalse(gesture.Up(true, 330));
        gesture.Down(true, false, 400); Assert.IsFalse(gesture.Up(true, 450));
        gesture.Down(true, false, 500); Assert.IsFalse(gesture.Up(true, 900));
        gesture.Down(true, true, 1000); Assert.IsFalse(gesture.Up(true, 1050));
    }
}
