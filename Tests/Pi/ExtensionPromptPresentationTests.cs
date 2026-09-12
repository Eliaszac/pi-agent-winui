using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class ExtensionPromptPresentationTests
{
    [TestMethod]
    public void PermissionPreviewPreservesCommandAndGenericQuestionsStayIntact()
    {
        var prompt = new ExtensionPrompt("id", "select", "Allow bash? echo 'hello? world'\nls", "", ["Allow", "Block"], "", null);
        var (heading, preview) = ExtensionPromptPresentation.Split(prompt);
        Assert.AreEqual("Allow bash?", heading);
        Assert.AreEqual("echo 'hello? world'\nls", preview);
        var generic = prompt with { Options = ["Yes", "No"] };
        Assert.AreEqual((generic.Title, ""), ExtensionPromptPresentation.Split(generic));
    }

    [TestMethod]
    public async Task ChoiceIsExplicitAndOriginalValueIsSent()
    {
        System.Text.Json.Nodes.JsonObject? reply = null;
        var prompt = new ExtensionPrompt("id", "select", "Allow bash? ls", "", ["Allow", "Allow always (global)", "Block"], "", null);
        using var model = new ExtensionPromptViewModel(prompt, (_, response) => { reply = response; return Task.CompletedTask; }, _ => { }, _ => { });
        Assert.IsFalse(model.CanSubmit);
        await model.SubmitCommand.ExecuteAsync(null);
        Assert.IsNull(reply);
        model.Value = "Allow always (global)";
        Assert.IsTrue(model.CanSubmit);
        await model.SubmitCommand.ExecuteAsync(null);
        Assert.AreEqual("Allow always (global)", reply!["value"]!.GetValue<string>());
        Assert.IsFalse(model.CanSubmit);
    }
}
