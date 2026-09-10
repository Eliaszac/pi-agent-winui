using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class LspDiagnosticsTests
{
    [DataTestMethod]
    [DataRow("{\"receivedResponse\":true,\"diagnostics\":[]}", true)]
    [DataRow("{\"receivedResponse\":false,\"diagnostics\":[]}", false)]
    [DataRow("{\"receivedResponse\":true,\"diagnostics\":[{}]}", false)]
    [DataRow("{\"receivedResponse\":true,\"unsupported\":true,\"diagnostics\":[]}", false)]
    public void RequiresCompleteUnambiguousResponse(string result, bool expected)
    {
        using var input = JsonDocument.Parse("{\"action\":\"diagnostics\",\"file\":\"a.ts\"}");
        using var details = JsonDocument.Parse(result);
        Assert.AreEqual(expected, LspDiagnosticsParser.Parse("lsp", input.RootElement, details.RootElement) is not null);
    }

    [TestMethod]
    public void CountsOnlyRespondingFilesAndChangedPathsAndInvalidatesAfterWrite()
    {
        using var input = JsonDocument.Parse("{\"action\":\"workspace-diagnostics\"}");
        using var details = JsonDocument.Parse("{\"items\":[{\"file\":\"a.ts\",\"status\":\"ok\",\"diagnostics\":[{\"severity\":1},{\"severity\":2}]},{\"file\":\"other.ts\",\"status\":\"ok\",\"diagnostics\":[{\"severity\":1}]},{\"file\":\"b.ts\",\"status\":\"timeout\",\"diagnostics\":[]}]}");
        var tracker = new RunVerificationTracker();
        tracker.Observe(new("lsp", "lsp", "", Status: "Completed", IsTool: true,
            Diagnostics: LspDiagnosticsParser.Parse("lsp", input.RootElement, details.RootElement)));
        FileChange[] changes = [new("a.ts", null, 1, 0, null), new("b.ts", null, 1, 0, null)];
        Assert.AreEqual("Diagnostics · 1 error · 1 warning · 1/2 files checked", tracker.DiagnosticsLabel(changes, null));
        Assert.IsNull(tracker.DiagnosticsLabel([], null));
        Assert.IsFalse(new RunChangesViewModel([], diagnosticsLabel: "test").HasDiagnostics);
        tracker.Observe(new("write", "write", "", Status: "Running", IsTool: true));
        Assert.IsNull(tracker.DiagnosticsLabel(changes, null));
    }
}
