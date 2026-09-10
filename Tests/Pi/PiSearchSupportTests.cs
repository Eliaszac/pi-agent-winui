using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;
using PiAgentGui.Services.Pi;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiSearchSupportTests
{
    [TestMethod]
    public void WorkerOnlyLoadsRegisteredSupportedCompleteSearchPackage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            Assert.IsNull(PiSearchSupport.FindWorkerExtension(root));
            File.WriteAllText(Path.Combine(root, "settings.json"), """{"packages":[{"source":"npm:@heyhuynhgiabuu/pi-search@0.3.0"}]}""");
            var package = Path.Combine(root, "npm", "node_modules", "@heyhuynhgiabuu", "pi-search");
            Directory.CreateDirectory(Path.Combine(package, "dist"));
            var manifest = Path.Combine(package, "package.json");
            File.WriteAllText(manifest, """{"name":"@heyhuynhgiabuu/pi-search","version":"0.3.0"}""");
            Assert.IsNull(PiSearchSupport.FindWorkerExtension(root));
            var entry = Path.Combine(package, "dist", "index.js");
            File.WriteAllText(entry, "");
            Assert.AreEqual(entry, PiSearchSupport.FindWorkerExtension(root));
            var executable = Path.Combine(root, "pi.exe"); File.WriteAllText(executable, "");
            var info = new PiProcessStartInfoFactory(new PiRuntimeOptions { ExecutablePath = executable }, () => PiSearchSupport.FindWorkerExtension(root))
                .Create(new PiLaunchRequest(root, Path.Combine(root, "session.jsonl"), ResearchWorker: true, Provider: "test", Model: "test"));
            CollectionAssert.Contains(info.ArgumentList.ToList(), entry);
            CollectionAssert.Contains(info.ArgumentList.ToList(), "read,grep,find,ls,read_web_page,websearch");
            StringAssert.Contains(info.Environment["PI_SEARCH_DISABLED_TOOLS"]!, "firecrawl_crawl");
            File.WriteAllText(manifest, """{"name":"@heyhuynhgiabuu/pi-search","version":"0.4.0"}""");
            Assert.IsNull(PiSearchSupport.FindWorkerExtension(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
