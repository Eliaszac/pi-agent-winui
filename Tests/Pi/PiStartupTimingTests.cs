using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiStartupTimingTests
{
    [TestMethod]
    public void CapturesHandlerTimingsAndRejectsPaths()
    {
        var values = new List<(string, double)>();
        var parser = new PiStartupTimingParser((stage, ms) => values.Add((stage, ms)));
        parser.Append("PI_GUI_HANDLER session_start.2.checkpoints.ts.0 1820\nPI_GUI_HANDLER session_start.C:/private 4\n");
        Assert.AreEqual(1, values.Count);
        Assert.AreEqual(("pi.handler.session_start.2.checkpoints.ts.0", 1820d), values[0]);
    }
    [TestMethod]
    public void CapturesChunkedTimingsWithoutGeneralStderrOrPaths()
    {
        var values = new List<(string Stage, double Milliseconds)>();
        var parser = new PiStartupTimingParser((stage, ms) => values.Add((stage, ms)));
        parser.Append("Secret diagnostic\n--- Startup Timings: ma");
        parser.Append("in ---\r\n  createAgentSession: 123ms\n  TOTAL: 123ms\n-------------------\n  Secret: 42ms\n");
        parser.Append("--- Startup Timings: extensions ---\n  C:/private/extension.ts: 50ms\n----------------\n");
        Assert.AreEqual(3, values.Count);
        Assert.AreEqual(("pi.startup.main.createAgentSession", 123d), values[0]);
        Assert.AreEqual(("pi.startup.extensions.entry-0", 50d), values[2]);
        Assert.IsFalse(values.Any(value => value.Stage.Contains("private") || value.Stage.Contains("Secret")));
    }
}
