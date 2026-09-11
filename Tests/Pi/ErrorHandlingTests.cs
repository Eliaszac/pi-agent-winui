using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ErrorHandlingTests
{
#if DEBUG
    [TestMethod]
    public async Task ErrorPreviewUsesRealPresentationWithoutSendingOrRemovingAttachments()
    {
        var dispatcher = new QueuedUiDispatcher();
        var session = new FakeConversationSession();
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync(); dispatcher.Drain();
        model.AddScreenshot(new ChatImage("aGVsbG8="));
        model.Draft = "/test-error";
        await model.SendCommand.ExecuteAsync();
        Assert.IsTrue(model.HasInlineError);
        Assert.AreEqual("Provider limit reached", model.ErrorTitle);
        StringAssert.Contains(model.ErrorDiagnosticReport, "UI PREVIEW");
        Assert.IsTrue(model.HasPendingImages);
        Assert.AreEqual(0, session.Sent.Count);
        Assert.AreEqual(0, session.Steered.Count);
        model.Draft = "/test-error clear";
        await model.SendCommand.ExecuteAsync();
        Assert.IsFalse(model.HasError);
        Assert.AreEqual(0, session.Sent.Count);
    }
#endif
    [DataTestMethod]
    [DataRow("Pi did not acknowledge the request. Its acceptance is uncertain", "acknowledgement-timeout")]
    [DataRow("401 Unauthorized", "authentication")]
    [DataRow("429 rate limit exceeded", "provider-limit")]
    [DataRow("Cannot find module private/file", "extension")]
    [DataRow("Pi exited (exit code 1)", "process-exit")]
    [DataRow("Pi sent invalid JSON", "protocol")]
    public void ErrorsProvideSpecificRecoveryGuidance(string message, string category)
    {
        var description = ConversationErrors.Describe(message);
        Assert.AreEqual(category, description.Category);
        Assert.IsTrue(description.Help.Length > 30);
    }

    [TestMethod]
    public void VisibleErrorsRedactCommonCredentialFormatsAndBoundLongMessages()
    {
        var message = ConversationErrors.Describe("401 api_key=private-key Bearer private-bearer https://person:private-password@example.com/?access_token=private-token").Message;
        foreach (var secret in new[] { "private-key", "private-bearer", "private-password", "private-token" }) Assert.IsFalse(message.Contains(secret));
        Assert.IsTrue(ConversationErrors.Describe(new string('x', 20_000)).Message.Length < 4100);
    }

    [TestMethod]
    public void DiagnosticReportsExcludeMessagesAndExceptionData()
    {
        var exception = new IOException("secret-user-prompt", new InvalidOperationException("secret-token"));
        exception.Data["session"] = "private-session-id";
        var report = ErrorDiagnostics.Create(exception);
        Assert.IsFalse(report.Contains("secret-user-prompt")); Assert.IsFalse(report.Contains("secret-token")); Assert.IsFalse(report.Contains("private-session-id"));
        StringAssert.Contains(report, "System.IO.IOException"); StringAssert.Contains(report, "System.InvalidOperationException");
        StringAssert.Contains(report, "Runtime:"); StringAssert.Contains(report, "Report:");
        StringAssert.Contains(ErrorDiagnostics.Create(new PiProcessExitException(17, "private stderr")), "Pi exit code: 17");
    }

    [TestMethod]
    public void ExitHintsRecognizeSplitDependencyMessagesWithoutReturningRawStderr()
    {
        var hint = new PiExitHint();
        hint.Append("secret-token ERR_PACKAGE_PATH_"); hint.Append("NOT_EXPORTED C:/private/package");
        StringAssert.Contains(hint.Describe(), "dependency");
        Assert.IsFalse(hint.Describe().Contains("secret-token")); Assert.IsFalse(hint.Describe().Contains("C:/private"));
        hint.Append(new string('x', 9000));
        Assert.IsFalse(hint.Describe().Contains("dependency"));
    }

    [TestMethod]
    public async Task RecoveryDetailsStayWithTheirConversationAndDoNotResendDrafts()
    {
        var dispatcher = new QueuedUiDispatcher(); var session = new FakeConversationSession();
        await using var model = new ConversationViewModel(session, dispatcher);
        await model.InitializeAsync(); dispatcher.Drain();
        model.Draft = "keep this prompt";
        var report = ErrorDiagnostics.Create(new IOException("private original error"));
        session.Emit(new ConversationUpdate { IsConnected = false, IsRunning = false, Error = "Pi exited", ErrorDiagnostics = report });
        dispatcher.Drain();
        Assert.AreEqual("Pi stopped unexpectedly", model.ErrorTitle);
        StringAssert.Contains(model.ErrorDiagnosticReport, report);
        Assert.IsFalse(model.ErrorDiagnosticReport.Contains("keep this prompt"));
        Assert.AreEqual("keep this prompt", model.Draft); Assert.AreEqual(0, session.Sent.Count);
        await model.RetryCommand.ExecuteAsync(); dispatcher.Drain();
        Assert.IsFalse(model.HasError); Assert.AreEqual("keep this prompt", model.Draft); Assert.AreEqual(0, session.Sent.Count);
    }
}
