using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class FileChangeTests
{
    [TestMethod]
    public void PatchCountsIgnoreHeadersButIncludeContentStartingWithPlusOrMinus()
    {
        using var input = JsonDocument.Parse("""{"path":"src/file.cs"}""");
        using var details = JsonDocument.Parse(JsonSerializer.Serialize(new { patch = "--- old\n+++ new\n@@ -1,2 +1,3 @@\n-old\n+++content\n+new\n same\n" }));
        var change = FileChangeParser.Parse("edit", input.RootElement, details.RootElement)!;
        Assert.AreEqual("src/file.cs", change.Path);
        Assert.AreEqual(2, change.Added);
        Assert.AreEqual(1, change.Removed);
    }

    [TestMethod]
    public void SavedWriteRestoresCapturedPatchAndLegacyWriteDoesNotInventDeletions()
    {
        var transcript = new PiTranscript();
        using var history = JsonDocument.Parse("""
            [{"role":"assistant","timestamp":1,"content":[{"type":"toolCall","id":"w1","name":"write","arguments":{"path":"file.txt","content":"new\n"}}]},
             {"role":"toolResult","toolCallId":"w1","toolName":"write","content":[{"type":"text","text":"Successfully wrote"}],"details":{"piGuiFileChange":{"version":1,"path":"file.txt","patch":"--- file.txt\n+++ file.txt\n@@ -1 +1 @@\n-old\n+new\n"}}},
             {"role":"assistant","timestamp":2,"content":[{"type":"toolCall","id":"w2","name":"write","arguments":{"path":"legacy.txt","content":"unknown before"}}]},
             {"role":"toolResult","toolCallId":"w2","toolName":"write","content":[{"type":"text","text":"Successfully wrote"}]}]
            """);
        var entries = transcript.Load(history.RootElement);
        var write = entries.Single(entry => entry.Id == "tool:w1");
        Assert.AreEqual(1, write.FileChange!.Added);
        Assert.AreEqual(1, write.FileChange.Removed);
        var legacy = entries.Single(entry => entry.Id == "tool:w2");
        Assert.AreEqual("legacy.txt", legacy.FileChange!.Path);
        Assert.IsNull(legacy.FileChange.Patch);
        Assert.IsNotNull(legacy.FileChange.Unavailable);
    }
}
