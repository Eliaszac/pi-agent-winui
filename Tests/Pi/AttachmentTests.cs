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
    [DataRow("@issues:", "issues", "")]
    [DataRow("@issues:42", "issues", "42")]
    [DataRow("@prs:fix login", "prs", "fix login")]
    [DataRow("@files:My Folder", "files", "My Folder")]
    [DataRow("@ISSUES: login", "issues", "login")]
    public void ScopedReferencesSelectSourceAndAllowSpaces(string input, string kind, string query)
    {
        var text = "Review " + input;
        var token = FileReferenceToken.Find(text, text.Length)!;
        Assert.AreEqual(kind, token.Kind);
        Assert.AreEqual(query, token.Query);
        Assert.AreEqual("Review ", text.Remove(token.Start, token.Length));
    }

    [TestMethod]
    public void ScopedReferencesRespectLineAndReferenceBoundaries()
    {
        const string multiline = "@issues:bug\ntext";
        Assert.IsNull(FileReferenceToken.Find(multiline, multiline.Length));
        var text = "@issues:bug @src";
        var token = FileReferenceToken.Find(text, text.Length)!;
        Assert.IsNull(token.Kind);
        Assert.AreEqual("src", token.Query);
        Assert.IsNull(FileReferenceToken.Find("@prs:login", 10, 1));
        const string middle = "Review @prs:fix login later";
        var scoped = FileReferenceToken.Find(middle, middle.IndexOf("login") + 2)!;
        Assert.AreEqual("Review  later", middle.Remove(scoped.Start, scoped.Length));
    }

    [TestMethod]
    public void AttachmentLimitsRejectOversizedAndExcessImages()
    {
        Assert.ThrowsException<ArgumentException>(() => PiImageContent.Serialize(Enumerable.Repeat(new ChatImage("AQID"), 5).ToArray()));
        Assert.ThrowsException<ArgumentException>(() => PiImageContent.Serialize([new ChatImage(Convert.ToBase64String(new byte[PiImageContent.MaximumImageBytes + 1]))]));
    }
}
