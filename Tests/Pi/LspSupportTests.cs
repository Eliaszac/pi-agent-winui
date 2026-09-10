using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class LspSupportTests
{
    [TestMethod]
    public void RejectsIncompatibleHoistedDependencyAndAcceptsNestedPin()
    {
        var root = Path.Combine(Path.GetTempPath(), "pi-lsp-test-" + Guid.NewGuid());
        var modules = Path.Combine(root, "npm", "node_modules");
        var lsp = Path.Combine(modules, "lsp-pi");
        Directory.CreateDirectory(lsp);
        try
        {
            File.WriteAllText(Path.Combine(root, "settings.json"), "{\"packages\":[\"npm:lsp-pi@1.0.5\"]}");
            File.WriteAllText(Path.Combine(lsp, "package.json"), "{\"name\":\"lsp-pi\",\"version\":\"1.0.5\"}");
            foreach (var file in new[] { "lsp.ts", "lsp-tool.ts", "lsp-core.ts" }) File.WriteAllText(Path.Combine(lsp, file), "");
            Assert.IsTrue(LspSupport.GetInstallationState(root).NeedsSetup);
            var hoisted = Path.Combine(modules, "vscode-languageserver-protocol");
            Directory.CreateDirectory(hoisted);
            File.WriteAllText(Path.Combine(hoisted, "package.json"), "{\"version\":\"3.18.3\"}");
            Assert.IsTrue(LspSupport.GetInstallationState(root).NeedsSetup);
            var nested = Path.Combine(lsp, "node_modules", "vscode-languageserver-protocol");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "package.json"), "{\"version\":\"3.17.5\"}");
            Assert.IsFalse(LspSupport.GetInstallationState(root).NeedsSetup);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
