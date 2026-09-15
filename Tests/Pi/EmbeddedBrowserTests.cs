using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class EmbeddedBrowserTests
{
    [TestMethod]
    [DataRow("", "https://www.google.com/")]
    [DataRow("  native UI design  ", "https://www.google.com/search?q=native%20UI%20design")]
    [DataRow("C# & XAML", "https://www.google.com/search?q=C%23%20%26%20XAML")]
    [DataRow("example.com/docs", "http://example.com/docs")]
    [DataRow("localhost:5173", "http://localhost:5173/")]
    [DataRow("127.0.0.1:3000", "http://127.0.0.1:3000/")]
    [DataRow("https://example.com", "https://example.com/")]
    public void AddressBarDistinguishesSearchesFromWebAddresses(string input, string expected) =>
        Assert.AreEqual(expected, BrowserAddress.FromInput(input).AbsoluteUri);

    [TestMethod]
    public void BrowserTabsAreIndependentAndRetainTitlesAndSelection()
    {
        var first = new SidePanelState(); var second = new SidePanelState();
        var a = first.Open("browser", "Browser"); var b = first.Open("browser", "Browser");
        a.Title = "Documentation"; b.Title = "Preview";
        second.Open("browser", "Other conversation");
        first.Tabs.Move(1, 0); first.Close(b);
        Assert.AreSame(a, first.Selected);
        Assert.AreEqual("Documentation", a.Title);
        Assert.AreEqual(1, second.Tabs.Count);
    }

    [TestMethod]
    [DataRow("https://example.com/test", "https://example.com/test")]
    [DataRow("localhost:5173", "http://localhost:5173/")]
    [DataRow("about:blank", "about:blank")]
    public void BrowserAddressesAllowWebPreviews(string input, string expected) => Assert.AreEqual(expected, BrowserAddress.Parse(input).AbsoluteUri);

    [TestMethod]
    [DataRow("file:///C:/secret")]
    [DataRow("javascript://alert(1)")]
    [DataRow("https://user:secret@example.com")]
    public void BrowserRejectsUnsafeAddresses(string input) => Assert.ThrowsException<ArgumentException>(() => BrowserAddress.Parse(input));

    [TestMethod]
    public void ForwardingUsesExplicitTargetsAndHiddenProcesses()
    {
        var ssh = new ExecutionTarget { Id = Guid.NewGuid(), Name = "SSH", Kind = "ssh", Host = "test-server", Path = "/workspace" };
        var start = BrowserPreviewTunnel.CreateStartInfo(ssh, 5173);
        Assert.IsTrue(start.CreateNoWindow); Assert.IsFalse(start.UseShellExecute);
        CollectionAssert.Contains(start.ArgumentList.ToArray(), "127.0.0.1:5173");
        Assert.AreEqual("test-server", start.ArgumentList.Last());
        var wsl = BrowserPreviewTunnel.CreateStartInfo(ssh with { Kind = "wsl", Host = "Ubuntu" }, 3000);
        CollectionAssert.Contains(wsl.ArgumentList.ToArray(), "Ubuntu");
        CollectionAssert.Contains(wsl.ArgumentList.ToArray(), "python3");
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BrowserPreviewTunnel.CreateStartInfo(ssh, -1));
    }
}
