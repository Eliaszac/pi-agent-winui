using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

[TestClass]
public sealed class UntrackedFileStatsTests
{
    [TestMethod]
    public async Task CountsFinalUnterminatedLineAndDetectsBinary()
    {
        var root = Path.Combine(Path.GetTempPath(), "pi-git-stats-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "text.txt"), "one\ntwo");
            var text = await UntrackedFileStats.ReadAsync(root, new("text.txt", '?', false, null, null), default);
            Assert.AreEqual(2L, text.Added); Assert.AreEqual(0L, text.Removed);
            await File.WriteAllBytesAsync(Path.Combine(root, "binary.bin"), [1, 0, 2]);
            var binary = await UntrackedFileStats.ReadAsync(root, new("binary.bin", '?', false, null, null), default);
            Assert.IsTrue(binary.Binary); Assert.IsNull(binary.Added);
            var outside = new GitChange("../outside.txt", '?', false, null, null);
            Assert.AreSame(outside, await UntrackedFileStats.ReadAsync(root, outside, default));
        }
        finally { Directory.Delete(root, true); }
    }
}
