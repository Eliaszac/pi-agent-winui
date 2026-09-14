using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Conversations;

[TestClass]
public sealed class TranscriptScrollPolicyTests
{
    [TestMethod]
    public void EstimatedHistoryExtentDoesNotChooseScrollDestination()
        => Assert.AreEqual(1120d, TranscriptScrollPolicy.TailOffset(1000, 720, 600, 5000));

    [TestMethod]
    public void GrowingResponseFollowsOnlyItsMeasuredGrowth()
        => Assert.AreEqual(1080d, TranscriptScrollPolicy.TailOffset(1000, 680, 600, 2000));

    [TestMethod]
    public void RemovingProcessingRowLeavesUpwardCorrectionToNativeAnchoring()
        => Assert.AreEqual(1000d, TranscriptScrollPolicy.TailOffset(1000, 570, 600, 2000));

    [TestMethod]
    public void StaleRowGeometryCannotPullViewportBackToPreviousResponse()
        => Assert.AreEqual(3000d, TranscriptScrollPolicy.TailOffset(3000, -1200, 600, 5000));

    [TestMethod]
    public void UnrealizedTailCannotResumeFollowingUsingEstimatedExtent()
        => Assert.IsFalse(TranscriptScrollPolicy.ShouldResumeFollowing(true, 900, 1000, double.PositiveInfinity));

    [TestMethod]
    public void ShortTranscriptNeverRequestsNegativeOffset()
        => Assert.AreEqual(0d, TranscriptScrollPolicy.TailOffset(0, 200, 600, 0));

    [TestMethod]
    public void LayoutShrinkingToReaderDoesNotResumeFollowing()
        => Assert.IsFalse(TranscriptScrollPolicy.ShouldResumeFollowing(false, 500, 500, 500));

    [TestMethod]
    public void UserScrollingUpNearBottomStaysDetached()
        => Assert.IsFalse(TranscriptScrollPolicy.ShouldResumeFollowing(true, 1000, 980, 1000));

    [TestMethod]
    public void UserScrollingDownToBottomResumesFollowing()
        => Assert.IsTrue(TranscriptScrollPolicy.ShouldResumeFollowing(true, 900, 990, 1000));

    [TestMethod]
    public void UserScrollingDownThroughHistoryStaysDetached()
        => Assert.IsFalse(TranscriptScrollPolicy.ShouldResumeFollowing(true, 500, 600, 1000));

    [TestMethod]
    public void ProgrammaticMoveToBottomDoesNotResumeFollowing()
        => Assert.IsFalse(TranscriptScrollPolicy.ShouldResumeFollowing(false, 500, 1000, 1000));
}
