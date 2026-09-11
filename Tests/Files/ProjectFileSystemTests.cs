using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Files;
using PiAgentGui.Tests.Pi;
using PiAgentGui.ViewModels.Files;

namespace PiAgentGui.Tests.Files;

[TestClass]
public sealed class ProjectFileSystemTests
{
    private string root = "";
    private ProjectFileSystem files = null!;
    private string? recycled;
    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(Path.GetTempPath(), "pi-explorer-test-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        files = new(root, (path, _) => recycled = path);
    }
    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void CreatesRenamesMovesAndRequestsRecycleWithoutOverwrites()
    {
        var folder = files.Create(root, "folder", true);
        var file = files.Create(root, "test.cs", false);
        File.WriteAllText(file, "preserve content");
        file = files.Rename(file, "renamed.cs");
        file = files.Move(file, folder);
        Assert.AreEqual("preserve content", File.ReadAllText(file));
        Assert.ThrowsException<IOException>(() => files.Create(folder, "renamed.cs", false));
        files.Delete(file);
        Assert.AreEqual(file, recycled);
        Assert.IsTrue(File.Exists(file));
    }

    [DataTestMethod]
    [DataRow("../escape")][DataRow("CON")][DataRow("a/b")][DataRow("name.")][DataRow("name ")][DataRow("NUL.txt")][DataRow("x:y")]
    public void RejectsUnsafeNames(string name) => Assert.ThrowsException<IOException>(() => files.Create(root, name, false));

    [TestMethod]
    public void RejectsRootDeletionOutsidePathsAndMovingFolderIntoItself()
    {
        Assert.ThrowsException<IOException>(() => files.Delete(root));
        Assert.ThrowsException<IOException>(() => files.Validate(root + "-sibling"));
        var folder = files.Create(root, "folder", true);
        var child = files.Create(folder, "child", true);
        Assert.ThrowsException<IOException>(() => files.Move(folder, child));
        Assert.IsTrue(Directory.Exists(folder));
        var a = files.Create(root, "a.txt", false);
        var b = files.Create(root, "b.txt", false);
        Assert.ThrowsException<IOException>(() => files.Rename(a, "b.txt"));
        Assert.IsTrue(File.Exists(a)); Assert.IsTrue(File.Exists(b));
    }

    [TestMethod]
    public void CaseOnlyRenamePreservesFileContent()
    {
        var path = files.Create(root, "lower.txt", false);
        File.WriteAllText(path, "content");
        var result = files.Rename(path, "LOWER.txt");
        Assert.AreEqual("LOWER.txt", new DirectoryInfo(root).GetFiles().Single().Name);
        Assert.AreEqual("content", File.ReadAllText(result));
    }

    [TestMethod]
    public async Task CreatePlaceholderDoesNotCollideWithAnExistingNewFile()
    {
        files.Create(root, "New file", false);
        using var model = new FileExplorerViewModel(new QueuedUiDispatcher());
        model.SelectProject(root); await model.RefreshAsync();
        await model.BeginCreateAsync(model.Items[0], false);
        await model.CommitEditAsync();
        Assert.IsTrue(model.HasError);
        Assert.IsNotNull(model.Editing);
        model.Editing.Draft = "another.txt";
        await model.CommitEditAsync();
        Assert.IsFalse(model.HasError);
        Assert.IsNull(model.Editing);
        Assert.IsTrue(model.Items.Any(item => item.Name == "another.txt"));
    }

    [TestMethod]
    public void SnapshotOpensRootOnlyAndOrdersFoldersFirst()
    {
        var folder = files.Create(root, "z-folder", true);
        files.Create(folder, "nested.txt", false);
        files.Create(root, "a.txt", false);
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        var rows = ExplorerSnapshot.Read(files, expanded);
        Assert.AreEqual(3, rows.Count);
        Assert.IsTrue(rows[0].IsExpanded);
        Assert.IsFalse(rows[1].IsExpanded);
        Assert.AreEqual("z-folder", rows[1].Name);
        expanded.Add(folder);
        Assert.AreEqual(4, ExplorerSnapshot.Read(files, expanded).Count);
    }

    [TestMethod]
    public async Task TogglePreservesUnrelatedRowsAndCollapseNeedsNoDirectoryRead()
    {
        var folder = files.Create(root, "folder", true);
        files.Create(folder, "child.txt", false);
        files.Create(root, "outside.txt", false);
        using var model = new FileExplorerViewModel(new QueuedUiDispatcher());
        model.SelectProject(root); await model.RefreshAsync();
        var unrelated = model.Items.Single(item => item.Name == "outside.txt");
        await model.ToggleAsync(model.Items.Single(item => item.Name == "folder"));
        await model.ToggleAsync(model.Items.Single(item => item.Name == "folder"));
        Assert.AreSame(unrelated, model.Items.Single(item => item.Name == "outside.txt"));
        Assert.IsTrue(model.Items.Any(item => item.Name == "child.txt"));
        Directory.Delete(folder, true);
        var collapse = model.ToggleAsync(model.Items.Single(item => item.Name == "folder"));
        Assert.IsTrue(collapse.IsCompletedSuccessfully);
        Assert.IsFalse(model.Items.Any(item => item.Name == "child.txt"));
        Assert.IsFalse(model.HasError);
    }

    [TestMethod]
    public async Task RapidTogglesAreNotDroppedAndKeepFolderIdentity()
    {
        var folder = files.Create(root, "folder", true);
        files.Create(folder, "child.txt", false);
        using var model = new FileExplorerViewModel(new QueuedUiDispatcher());
        model.SelectProject(root); await model.RefreshAsync();
        var row = model.Items.Single(item => item.Name == "folder");
        await model.ToggleAsync(row);
        var opening = model.ToggleAsync(row);
        var closing = model.ToggleAsync(row);
        await Task.WhenAll(opening, closing);
        Assert.AreSame(row, model.Items.Single(item => item.Name == "folder"));
        Assert.IsFalse(row.IsExpanded);
        Assert.IsFalse(model.Items.Any(item => item.Name == "child.txt"));
        await model.ToggleAsync(row);
        Assert.IsTrue(row.IsExpanded);
        Assert.IsTrue(model.Items.Any(item => item.Name == "child.txt"));
    }

    [TestMethod]
    public async Task DefaultsOpenSmallFirstLevelFoldersOnlyAndRespectManualCollapse()
    {
        var small = files.Create(root, "small", true);
        var nested = files.Create(small, "nested", true);
        files.Create(nested, "hidden.txt", false);
        for (var index = 0; index < 9; index++) files.Create(small, $"file{index}.txt", false);
        var large = files.Create(root, "large", true);
        for (var index = 0; index < 11; index++) files.Create(large, $"file{index}.txt", false);
        using var model = new FileExplorerViewModel(new QueuedUiDispatcher());
        model.SelectProject(root); await model.RefreshAsync();
        Assert.IsTrue(model.Items[0].IsExpanded);
        Assert.IsTrue(model.Items.Single(item => item.Path == small).IsExpanded);
        Assert.IsFalse(model.Items.Single(item => item.Path == large).IsExpanded);
        Assert.IsFalse(model.Items.Single(item => item.Path == nested).IsExpanded);
        Assert.IsFalse(model.Items.Any(item => item.Name == "hidden.txt"));
        await model.ToggleAsync(model.Items.Single(item => item.Path == small));
        await model.RefreshAsync();
        Assert.IsFalse(model.Items.Single(item => item.Path == small).IsExpanded);
    }

    [TestMethod]
    public async Task InlineCreateRenameAndExternalRefreshKeepExpandedFolder()
    {
        using var model = new FileExplorerViewModel(new QueuedUiDispatcher());
        model.SelectProject(root);
        await model.RefreshAsync();
        await model.BeginCreateAsync(model.Items[0], true);
        model.Editing!.Draft = "created";
        await model.CommitEditAsync();
        Assert.IsNull(model.Editing);
        var folder = model.Items.Single(item => item.Name == "created");
        await model.ToggleAsync(folder);
        File.WriteAllText(Path.Combine(root, "created", "external.txt"), "external");
        await model.RefreshAsync();
        Assert.IsTrue(model.Items.Any(item => item.Name == "external.txt"));
        var file = model.Items.Single(item => item.Name == "external.txt");
        model.BeginRename(file); model.Editing!.Draft = "renamed.txt";
        await model.CommitEditAsync();
        Assert.IsTrue(File.Exists(Path.Combine(root, "created", "renamed.txt")));
        Assert.IsFalse(model.HasError);
    }
}
