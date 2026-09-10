using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Applications;
using PiAgentGui.Services.Applications;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class OpenInTests
{
    private static readonly InstalledApplication Rider = new("rider", "Rider", @"C:\Apps\rider64.exe", "rider.svg", ApplicationKind.SolutionEditor);
    private static readonly InstalledApplication Code = new("vscode", "VS Code", @"C:\Apps\Code.exe", "vscode.png");
    private static readonly InstalledApplication Explorer = new("explorer", "File Explorer", @"C:\Windows\explorer.exe", "explorer.svg", ApplicationKind.Explorer);
    private static readonly InstalledApplication Terminal = new("terminal", "Windows Terminal", @"C:\Apps\wt.exe", "terminal.svg", ApplicationKind.Terminal);

    [TestMethod]
    public void PreferredEditorWinsAndMissingPreferenceFallsBack()
    {
        Assert.AreEqual(Code, OpenInSelection.Resolve([Rider, Code, Explorer], "vscode", Rider.ExecutablePath));
        Assert.AreEqual(Code, OpenInSelection.Resolve([Rider, Code, Explorer], "missing", Code.ExecutablePath));
        Assert.AreEqual(Rider, OpenInSelection.Resolve([Rider, Code, Explorer], "terminal", null));
        Assert.AreEqual(Explorer, OpenInSelection.Resolve([Terminal, Explorer], null, null));
    }

    [TestMethod]
    public void LaunchArgumentsPreservePathsWithoutShellParsing()
    {
        const string path = @"C:\Projects\hello world; example";
        var editor = ProjectApplicationLauncher.CreateStartInfo(Code, path);
        Assert.IsFalse(editor.UseShellExecute);
        CollectionAssert.AreEqual(new[] { path }, editor.ArgumentList.ToArray());
        var terminal = ProjectApplicationLauncher.CreateStartInfo(Terminal, path);
        Assert.AreEqual(path, terminal.WorkingDirectory);
        CollectionAssert.AreEqual(new[] { "-d", "." }, terminal.ArgumentList.ToArray());
    }

    [TestMethod]
    public void AmbiguousSolutionsAreNotChosenAndPreferencesRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "PiOpenInTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var solution = Path.Combine(root, "App.sln");
            File.WriteAllText(solution, "");
            Assert.AreEqual(solution, ProjectOpenTarget.FindSolution(root));
            Assert.AreEqual(solution, ProjectApplicationLauncher.CreateStartInfo(Rider, root).ArgumentList[0]);
            File.WriteAllText(Path.Combine(root, "Other.sln"), "");
            Assert.IsNull(ProjectOpenTarget.FindSolution(root));
            var preferences = new OpenInPreferenceStore(Path.Combine(root, "open-in.json"));
            Assert.IsNull(preferences.Read());
            preferences.Save("rider");
            preferences.Save("vscode");
            Assert.AreEqual("vscode", preferences.Read());
        }
        finally { Directory.Delete(root, true); }
    }
}
