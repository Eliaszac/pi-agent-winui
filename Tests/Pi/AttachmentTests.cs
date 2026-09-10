using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class AttachmentTests
{
    [TestMethod]
    public void FileLabelsKeepDistinctPathsAndHideDirectories()
    {
        var references = new PromptFileReferences();
        Assert.AreEqual("a.cs", references.Add("C:/private/a.cs"));
        Assert.AreEqual("a.cs (2)", references.Add("D:/other/a.cs"));
        Assert.AreEqual("Read @\"C:/private/a.cs\" and @\"D:/other/a.cs\"", references.Expand("Read @\"a.cs\" and @\"a.cs (2)\""));
        Assert.AreEqual("Read @a.cs", PromptFileReferences.Display("Read @\"C:/private/a.cs\""));
        Assert.AreEqual("Read @\"C:/private/a.cs\" and @\"D:/other/a.cs\"", references.Expand("Read @a.cs and @a.cs (2)"));
    }

    [TestMethod]
    public void ImagesSurviveHistoryAndLiveMessages()
    {
        var image = new ChatImage("AQID");
        var content = PiImageContent.Serialize([image]);
        using var message = JsonDocument.Parse("{\"role\":\"user\",\"timestamp\":1,\"content\":" + content + "}");
        var transcript = new PiTranscript();
        using var history = JsonDocument.Parse("[" + message.RootElement.GetRawText() + "]");
        var restored = transcript.Load(history.RootElement).Single();
        Assert.AreEqual(image, restored.Images!.Single());
        Assert.AreEqual("", restored.Text);
        using var packet = JsonDocument.Parse("{\"type\":\"message_end\",\"message\":" + message.RootElement.GetRawText() + "}");
        Assert.AreEqual(image, transcript.Apply(packet.RootElement)!.Images!.Single());
    }

    [TestMethod]
    public async Task ImageOnlySendClearsAcceptedImagesAndRetainsFailedImages()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var workspace = new ConversationViewModel(session, dispatcher);
        await workspace.InitializeAsync();
        dispatcher.Drain();
        var image = new ChatImage("AQID");
        workspace.AddScreenshot(image);
        Assert.IsTrue(workspace.CanSend);
        await workspace.SendCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.AreEqual(image, session.SentImages.Single().Single());
        Assert.IsFalse(workspace.HasPendingImages);
        session.Emit(new() { IsRunning = false });
        dispatcher.Drain();
        session.SendError = new IOException("Failed");
        workspace.AddScreenshot(image);
        await workspace.SendCommand.ExecuteAsync();
        dispatcher.Drain();
        Assert.AreEqual(image, workspace.PendingImages.Single());
    }

    [TestMethod]
    public void FileReferenceReplacesWholeTokenAndQuotesExternalPath()
    {
        const string text = "Read @source then explain";
        var token = FileReferenceToken.Find(text, 8)!;
        Assert.AreEqual("Read @\"D:/Other project/file.cs\"  then explain", token.Insert(text, @"D:\Other project\file.cs"));
        Assert.IsNull(FileReferenceToken.Find("a@example.com", 13));
        Assert.IsNull(FileReferenceToken.Find("@\"D:/file\"", 10));
    }

    [TestMethod]
    public void AttachmentLimitsRejectOversizedAndExcessImages()
    {
        Assert.ThrowsException<ArgumentException>(() => PiImageContent.Serialize(Enumerable.Repeat(new ChatImage("AQID"), 5).ToArray()));
        Assert.ThrowsException<ArgumentException>(() => PiImageContent.Serialize([new ChatImage(Convert.ToBase64String(new byte[PiImageContent.MaximumImageBytes + 1]))]));
    }
}
