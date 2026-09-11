using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class RunVerificationTests
{
    private const string Dotnet = "Passed!  - Failed: 0, Passed: 42, Skipped: 0, Total: 42, Duration: 1 s";

    [DataTestMethod]
    [DataRow("dotnet test Tests.csproj --no-restore", "tests")]
    [DataRow("npm run lint", "lint")]
    [DataRow("npx eslint .", "lint")]
    [DataRow("ruff check .", "lint")]
    [DataRow("npx vitest run", "tests")]
    [DataRow("dotnet build", "")]
    [DataRow("dotnet test Tests.csproj --no-restore && dotnet build App.csproj --no-restore", "tests")]
    [DataRow("dotnet test Tests.csproj && dotnet build App.csproj || true", "")]
    [DataRow("dotnet test Tests.csproj && dotnet build App.csproj && echo success", "")]
    [DataRow("dotnet test Tests.csproj && python change.py", "")]
    [DataRow("npm test || true", "")]
    [DataRow("npm test; echo success", "")]
    [DataRow("npx eslint . --fix", "")]
    [DataRow("echo 'dotnet test'", "")]
    public void RecognizesDirectChecksOnly(string command, string kind) =>
        Assert.AreEqual(kind, VerificationOutputParser.Kind("bash", JsonSerializer.Serialize(new { command })));

    [DataTestMethod]
    [DataRow(Dotnet, "Tests 42/42 passed")]
    [DataRow("Tests:       42 passed, 42 total", "Tests 42/42 passed")]
    [DataRow(" Tests  42 passed (42)", "Tests 42/42 passed")]
    [DataRow("================ 42 passed in 0.15s ================", "Tests 42/42 passed")]
    [DataRow("Tests: 1 failed, 41 passed, 42 total", null)]
    [DataRow("Passed! - Failed: 0, Passed: 41, Skipped: 1, Total: 42", null)]
    [DataRow("Everything passed, looks good", null)]
    [DataRow("Tests: 0 passed, 0 total", null)]
    public void CountsOnlyExplicitAllPassingSummaries(string output, string? expected) => Assert.AreEqual(expected, VerificationOutputParser.PassedTests(output));

    [TestMethod]
    public void ShowsBothChecksAfterEditsAndDropsThemAfterLaterMutation()
    {
        var tracker = new RunVerificationTracker();
        tracker.Observe(Edit("e1"));
        tracker.Observe(Check("t", "dotnet test", Dotnet));
        tracker.Observe(Check("l", "npm run lint", ""));
        CollectionAssert.AreEqual(new[] { "Lint passed", "Tests 42/42 passed" }, tracker.Labels.ToArray());
        // Duplicate message_end snapshots must not erase or count the result again.
        tracker.Observe(Edit("e1"));
        Assert.AreEqual(2, tracker.Labels.Count);
        tracker.Observe(Edit("e2"));
        Assert.AreEqual(0, tracker.Labels.Count);
    }

    [TestMethod]
    public void OverlappingWriteInvalidatesCheckAndFailedRetryDoesNotKeepPass()
    {
        var tracker = new RunVerificationTracker();
        tracker.Observe(Edit("e"));
        tracker.Observe(Check("t", "dotnet test", "", "Running"));
        tracker.Observe(Edit("later"));
        tracker.Observe(Check("t", "dotnet test", Dotnet));
        Assert.AreEqual(0, tracker.Labels.Count);
        tracker.Observe(Check("retry", "dotnet test", Dotnet));
        Assert.AreEqual(1, tracker.Labels.Count);
        tracker.Observe(Check("failure", "dotnet test", Dotnet, "Failed"));
        Assert.AreEqual(0, tracker.Labels.Count);
    }

    [TestMethod]
    public void UnknownShellCommandsAndRunResetClearVerification()
    {
        var tracker = new RunVerificationTracker();
        tracker.Observe(Check("l", "npm run lint", ""));
        tracker.Observe(Check("script", "python change.py", ""));
        Assert.AreEqual(0, tracker.Labels.Count);
        tracker.Observe(Check("l2", "npm run lint", ""));
        tracker.Reset();
        Assert.AreEqual(0, tracker.Labels.Count);
    }

    [TestMethod]
    public void TestThenBuildRetainsExplicitTotalsOnlyOnSuccess()
    {
        var tracker = new RunVerificationTracker();
        const string command = "dotnet test Tests/PiAgentGui.Tests.csproj --no-restore --verbosity minimal && dotnet build PiAgentGui.csproj --no-restore -p:Platform=x64 -p:OutDir=artifacts/verification/ --verbosity minimal";
        tracker.Observe(Edit("edit"));
        tracker.Observe(Check("check", command, Dotnet + "\nBuild succeeded.\n    0 Warning(s)\n    0 Error(s)"));
        CollectionAssert.AreEqual(new[] { "Tests 42/42 passed" }, tracker.Labels.ToArray());
        tracker.Observe(Check("failed-build", command, Dotnet + "\nBuild FAILED.", "Failed"));
        Assert.AreEqual(0, tracker.Labels.Count);
    }

    [TestMethod]
    public void VerificationDoesNotCreateAChangeSummaryByItself()
    {
        Assert.IsFalse(new RunChangesViewModel([], ["Lint passed"]).HasVerification);
        Assert.IsTrue(new RunChangesViewModel([new("a.cs", "patch", 1, 0, null)], ["Lint passed"]).HasVerification);
    }

    private static ChatEntry Edit(string id) => new(id, "write", "", Status: "Completed", IsTool: true, FileChange: new("a.cs", "patch", 1, 0, null));
    private static ChatEntry Check(string id, string command, string output, string status = "Completed") =>
        new(id, "bash", output, JsonSerializer.Serialize(new { command }), status, IsTool: true);
}
