using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiTranscriptTests
{
    [TestMethod]
    public void DeltaOnlyEventsAccumulateByContentIndexAndFinalMessageReplacesThem()
    {
        var transcript = new PiTranscript();
        var start = transcript.Apply(JsonDocument.Parse("""{"type":"message_start","message":{"role":"assistant","timestamp":100,"content":[]}}""").RootElement)!;
        transcript.Apply(JsonDocument.Parse("""{"type":"message_update","assistantMessageEvent":{"type":"text_delta","contentIndex":0,"delta":"Hello"}}""").RootElement);
        var partial = transcript.Apply(JsonDocument.Parse("""{"type":"message_update","assistantMessageEvent":{"type":"text_delta","contentIndex":0,"delta":" world"}}""").RootElement)!;
        Assert.AreEqual("Hello world", partial.Text);
        Assert.IsTrue(partial.IsAssistant);
        Assert.IsFalse(partial.IsComplete);
        var presentation = new PiAgentGui.ViewModels.Conversations.ChatEntryViewModel(partial);
        Assert.IsFalse(presentation.CanCopyResponse);
        Assert.AreEqual(start.Id, partial.Id);
        var final = transcript.Apply(JsonDocument.Parse("""{"type":"message_end","message":{"role":"assistant","timestamp":100,"content":[{"type":"text","text":"Final text"}]}}""").RootElement)!;
        Assert.AreEqual(start.Id, final.Id);
        Assert.AreEqual("Final text", final.Text);
        Assert.AreEqual("", final.Status);
        presentation.Update(final);
        Assert.IsTrue(presentation.CanCopyResponse);
        Assert.IsFalse(presentation.ShowSpeaker);
    }

    [TestMethod]
    public void HistoryPreservesUserAndCompletedAssistantPresentation()
    {
        var transcript = new PiTranscript();
        var history = transcript.Load(JsonDocument.Parse("""[{"role":"user","timestamp":1,"content":"full\nprompt"},{"role":"assistant","timestamp":2,"content":[{"type":"text","text":"full response"}]}]""").RootElement);
        var user = new PiAgentGui.ViewModels.Conversations.ChatEntryViewModel(history[0]);
        var assistant = new PiAgentGui.ViewModels.Conversations.ChatEntryViewModel(history[1]);
        Assert.IsTrue(user.IsUser);
        Assert.IsFalse(user.IsLeftAligned);
        Assert.IsTrue(user.CanCopyUser);
        Assert.AreEqual("full\nprompt", user.Text);
        Assert.IsTrue(assistant.CanCopyResponse);
        Assert.IsTrue(assistant.IsLeftAligned);
        Assert.IsFalse(assistant.HasHeader);
    }

    [TestMethod]
    public void ToolPartialResultsReplaceAccumulatedOutputAndPreserveArguments()
    {
        var transcript = new PiTranscript();
        transcript.Apply(JsonDocument.Parse("""{"type":"tool_execution_start","toolCallId":"t1","toolName":"bash","args":{"command":"pwd"}}""").RootElement);
        transcript.Apply(JsonDocument.Parse("""{"type":"tool_execution_update","toolCallId":"t1","toolName":"bash","partialResult":{"content":[{"type":"text","text":"a"}]}}""").RootElement);
        var partial = transcript.Apply(JsonDocument.Parse("""{"type":"tool_execution_update","toolCallId":"t1","toolName":"bash","partialResult":{"content":[{"type":"text","text":"ab"}]}}""").RootElement)!;
        Assert.AreEqual("ab", partial.Text);
        StringAssert.Contains(partial.Details, "pwd");
        var final = transcript.Apply(JsonDocument.Parse("""{"type":"tool_execution_end","toolCallId":"t1","toolName":"bash","result":{"content":[{"type":"text","text":"abc"}]},"isError":true}""").RootElement)!;
        Assert.AreEqual("abc", final.Text);
        Assert.AreEqual("Failed", final.Status);
        Assert.AreEqual(partial.Id, final.Id);
    }
}
