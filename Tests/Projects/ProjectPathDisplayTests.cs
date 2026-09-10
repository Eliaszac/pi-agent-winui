using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class ProjectPathDisplayTests
{
    [DataTestMethod]
    [DataRow(@"C:\Users\PrivateName\Projects\PiAgentGui", @"C:\…\PiAgentGui")]
    [DataRow(@"D:\Work\Project\", @"D:\…\Project")]
    [DataRow(@"C:\", @"C:\")]
    [DataRow(@"\\private-server\private-share\Team\Project", @"Network\…\Project")]
    [DataRow("", "")]
    public void RedactionHidesParentFoldersAndNetworkHost(string path, string expected) =>
        Assert.AreEqual(expected, ProjectPathDisplay.Redact(path));
}
