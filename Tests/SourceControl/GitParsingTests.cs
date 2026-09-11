using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class GitParsingTests
{
    [TestMethod]
    public void SeparatesIndexAndWorkingTreeAndPreservesUnusualPaths()
    {
        var path = "folder/tab\tline\nfile.cs";
        var rows = GitStatusParser.Parse($"MM {path}\0?? new file.txt\0 D removed.txt\0", $"4\t2\t{path}\0", $"1\t3\t{path}\0" + "0\t8\tremoved.txt\0");
        Assert.AreEqual(4, rows.Count);
        Assert.AreEqual(path, rows[0].Path);
        Assert.IsTrue(rows[0].Staged); Assert.AreEqual(4L, rows[0].Added);
        Assert.IsFalse(rows[1].Staged); Assert.AreEqual(3L, rows[1].Removed);
        Assert.AreEqual('?', rows[2].Status); Assert.IsNull(rows[2].Added);
        Assert.AreEqual(8L, rows[3].Removed);
    }

    [TestMethod]
    public void DistinguishesBinaryAndMergeConflictsFromOrdinaryChanges()
    {
        var rows = GitStatusParser.Parse("M  picture.png\0UU conflict.cs\0AA added.cs\0DD removed.cs\0", "-\t-\tpicture.png\0", "");
        Assert.IsTrue(rows[0].Binary); Assert.AreEqual("Binary", rows[0].Note);
        Assert.IsTrue(rows.Skip(1).All(row => row.Status == 'U' && !row.Staged));
    }

    [TestMethod]
    public void ReadsBranchUpstreamsAndSkipsRemoteHeadAliases()
    {
        var rows = GitBranchParser.Parse("refs/heads/main\0\0origin\0refs/heads/trunk\nrefs/remotes/origin/HEAD\0refs/remotes/origin/trunk\0\0\nrefs/remotes/origin/feature/ui\0\0\0\n", "refs/heads/main", ["origin"]);
        Assert.AreEqual(2, rows.Count);
        Assert.IsTrue(rows[0].Current); Assert.IsTrue(rows[0].CanFetch);
        Assert.AreEqual("refs/heads/trunk", rows[0].RemoteRef);
        Assert.IsTrue(rows[1].Remote); Assert.AreEqual("refs/heads/feature/ui", rows[1].RemoteRef);
        Assert.AreEqual("main", GitBranchParser.Parse("", "refs/heads/main", [])[0].Name);
    }

    [TestMethod]
    public void HidesCredentialsFromGitErrors()
    {
        var text = GitErrorMessage.Format("failed https://user:secret@example.com/repo?token=abc");
        Assert.IsFalse(text.Contains("secret")); Assert.IsFalse(text.Contains("abc"));
    }
}
