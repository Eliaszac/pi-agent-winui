using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Applications;
using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Applications;
using PiAgentGui.Services.Files;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Files;

[TestClass]
public sealed class WorkspaceFileLinkTests
{
    private static ExecutionTarget Target(string path, string kind = "local") => new() { Id = Guid.NewGuid(), Name = "Test", Path = path, Kind = kind, Host = "Ubuntu" };

    [TestMethod]
    public void RecognizesBareNamesPathsDotfilesAndLocations()
    {
        var matches = FileMentionParser.Find("I changed foo.js, src/bar/foo.js:42:7 and .gitignore. See README and app.cs#L12.");
        CollectionAssert.AreEqual(new[] { "foo.js", "src/bar/foo.js", ".gitignore", "README", "app.cs" }, matches.Select(m => m.Path).ToArray(), string.Join(" | ", matches.Select(m => m.Text)));
        Assert.AreEqual(42, matches[1].Line);
        Assert.AreEqual(7, matches[1].Column);
        Assert.AreEqual(12, matches[4].Line);
    }

    [TestMethod]
    public void LeavesUrlsEmailVersionsAndOrdinaryWordsAlone()
    {
        Assert.AreEqual(0, FileMentionParser.Find("Visit https://example.com/src/foo.js or person@example.org at version 1.2.3 today").Count);
    }

    [TestMethod]
    public void BareNamesRemainAmbiguousEvenWhenOneIsAtWorkspaceRoot()
    {
        var target = Target("C:/project");
        CollectionAssert.AreEqual(new[] { "foo.js", "src/foo.js" }, WorkspaceFileLinks.Match(target, ["foo.js", "src/foo.js", "other.js"], "foo.js").ToArray());
        CollectionAssert.AreEqual(new[] { "src/foo.js" }, WorkspaceFileLinks.Match(target, ["foo.js", "src/foo.js"], "src/foo.js").ToArray());
        CollectionAssert.AreEqual(new[] { "foo.js" }, WorkspaceFileLinks.Match(target, ["foo.js", "src/foo.js"], "C:/project/foo.js").ToArray());
        CollectionAssert.AreEqual(new[] { "foo.js" }, WorkspaceFileLinks.Match(target, ["foo.js", "src/foo.js"], "./foo.js").ToArray());
    }

    [TestMethod]
    public void ResolutionUsesTargetCaseRulesAndRejectsEscapes()
    {
        Assert.AreEqual(1, WorkspaceFileLinks.Match(Target("C:/project"), ["Foo.cs"], "foo.cs").Count);
        Assert.AreEqual(0, WorkspaceFileLinks.Match(Target("/work", "wsl"), ["Foo.cs"], "foo.cs").Count);
        Assert.AreEqual(0, WorkspaceFileLinks.Match(Target("C:/project"), ["foo.js"], "../foo.js").Count);
        Assert.AreEqual(0, WorkspaceFileLinks.Match(Target("C:/project"), ["foo.js"], "C:/elsewhere/foo.js").Count);
        Assert.AreEqual(1, WorkspaceFileLinks.Match(Target("/work", "ssh"), ["src/foo.js"], "/work/src/foo.js").Count);
    }

    [TestMethod]
    public async Task LocalDiscoveryExcludesDependenciesAndOpeningRechecksExistence()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "src", "foo.js"), "");
            await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "foo.js"), "");
            var opened = 0;
            using var links = new WorkspaceFileLinks(Target(root), (_, _, _, _) => { opened++; return Task.CompletedTask; });
            var mention = FileMentionParser.Find("foo.js:12").Single();
            var matches = await links.ResolveAsync(mention, CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "src/foo.js" }, matches.ToArray());
            await links.OpenAsync(matches[0], mention, CancellationToken.None);
            Assert.AreEqual(1, opened);
            await File.WriteAllTextAsync(Path.Combine(root, "foo.js"), "");
            Assert.AreEqual(2, (await links.ResolveAsync(mention, CancellationToken.None, refresh: true)).Count);
            File.Delete(Path.Combine(root, "src", "foo.js"));
            await Assert.ThrowsExceptionAsync<IOException>(() => links.OpenAsync(matches[0], mention, CancellationToken.None));
            Assert.AreEqual(1, opened);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void WindowsPathsAndBoundedParsingPreserveOriginalOffsets()
    {
        var text = @"Changed C:\project\src\foo.cs:3 and foo.js.";
        var mentions = FileMentionParser.Find(text);
        Assert.AreEqual(2, mentions.Count);
        Assert.AreEqual(@"C:\project\src\foo.cs", mentions[0].Path);
        foreach (var mention in mentions) Assert.AreEqual(mention.Text, text.Substring(mention.Start, mention.Length));
        Assert.AreEqual(0, FileMentionParser.Find(new string('a', 131073) + " foo.js").Count);
        Assert.AreEqual(256, FileMentionParser.Find(string.Concat(Enumerable.Repeat("foo.js ", 300))).Count);
    }

    [TestMethod]
    public async Task CancellationPreventsDiscoveryAndEditorActions()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var links = new WorkspaceFileLinks(Target("C:/not-a-workspace"), (_, _, _, _) => throw new AssertFailedException("Must not launch an editor."));
        var task = links.ResolveAsync(FileMentionParser.Find("foo.js").Single(), cancellation.Token);
        try { await task; Assert.Fail("Lookup must respect cancellation."); }
        catch (OperationCanceledException) { Assert.IsTrue(task.IsCanceled); }
    }

    [TestMethod]
    public void EditorArgumentsPreserveSpacesAndRemoteLocationsWithoutShellExpansion()
    {
        var code = new InstalledApplication("vscode", "Code", "C:/Code/Code.exe", "", ApplicationKind.Editor);
        var start = WorkspaceEditorLauncher.CreateStartInfo(code, Target("/work", "wsl"), "/work/my file.js", 42, 7);
        Assert.IsFalse(start.UseShellExecute);
        CollectionAssert.AreEqual(new[] { "--remote", "wsl+Ubuntu", "--goto", "/work/my file.js:42:7" }, start.ArgumentList.ToArray());
        var rider = code with { Id = "rider" };
        var local = WorkspaceEditorLauncher.CreateStartInfo(rider, Target("C:/work"), "C:/work/file.cs", 42, 7);
        CollectionAssert.AreEqual(new[] { "--line", "42", "--column", "7", "C:/work/file.cs" }, local.ArgumentList.ToArray());
        Assert.ThrowsException<IOException>(() => WorkspaceEditorLauncher.CreateStartInfo(rider, Target("/work", "ssh"), "/work/foo.cs", 1, null));
    }
}
