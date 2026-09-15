using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.GitHub;

[TestClass]
public sealed class PullRequestDetailsFormatterTests
{
    [DataTestMethod]
    [DataRow(-1, "just now")]
    [DataRow(0, "just now")]
    [DataRow(1, "1 minute ago")]
    [DataRow(59, "59 minutes ago")]
    [DataRow(60, "1 hour ago")]
    [DataRow(180, "3 hours ago")]
    [DataRow(1440, "1 day ago")]
    [DataRow(2880, "2 days ago")]
    public void FormatsRelativeTime(int minutes, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.AreEqual(expected, PullRequestDetailsFormatter.RelativeTime(now.AddMinutes(-minutes), now));
    }

    [TestMethod]
    public void FormatsMetadataAndOmitsMissingValues()
    {
        var now = DateTimeOffset.UtcNow;
        var pull = new GitHubPullRequest(123, "Title", new Uri("https://github.com/owner/repo/pull/123"));
        Assert.AreEqual("PR #123 · Open", PullRequestDetailsFormatter.Heading(pull));
        Assert.AreEqual("", PullRequestDetailsFormatter.Opened(pull, now));
        Assert.AreEqual("", PullRequestDetailsFormatter.Updated(pull, now));
        pull = pull with { Author = "octocat", IsDraft = true, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddHours(-3) };
        Assert.AreEqual("PR #123 · Draft", PullRequestDetailsFormatter.Heading(pull));
        Assert.AreEqual("Opened by @octocat · 2 days ago", PullRequestDetailsFormatter.Opened(pull, now));
        Assert.AreEqual("Updated 3 hours ago", PullRequestDetailsFormatter.Updated(pull, now));
        Assert.AreEqual("Opened 2 days ago", PullRequestDetailsFormatter.Opened(pull with { Author = null }, now));
        Assert.AreEqual("Opened by @octocat", PullRequestDetailsFormatter.Opened(pull with { CreatedAt = null }, now));
    }
}
