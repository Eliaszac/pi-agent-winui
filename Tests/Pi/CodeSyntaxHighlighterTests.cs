using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class CodeSyntaxHighlighterTests
{
    [DataTestMethod]
    [DataRow("csharp", "public class Test { string value = \"hello\"; }")]
    [DataRow("java", "public class Test { String value = \"hello\"; }")]
    [DataRow("javascript", "const value = \"hello\";")]
    [DataRow("typescript", "const value: string = \"hello\";")]
    [DataRow("python", "def hello():\n    return \"hello\"\n")]
    [DataRow("json", "{\"value\": 42}")]
    [DataRow("html", "<div class=\"hello\">Hello</div>")]
    [DataRow("css", "div { color: red; }")]
    [DataRow("powershell", "$value = \"hello\"")]
    [DataRow("bash", "echo \"hello\"")]
    [DataRow("svelte", "<script lang=\"ts\">\nlet value = $state(0);\n</script>\n{#if value}<p>{value}</p>{/if}\n<style>p { color: red; }</style>")]
    [DataRow("Component.svelte", "<p class=\"hello\">Hello</p>")]
    public void SupportedLanguagesPreserveCodeAndProduceColors(string language, string code)
    {
        var tokens = new CodeSyntaxHighlighter().Highlight(language, code);
        Assert.AreEqual(code, string.Concat(tokens.Select(token => token.Text)));
        Assert.IsTrue(tokens.Any(token => token.Kind != "plain"), language);
    }

    [TestMethod]
    public void StreamingAndRewritesMatchFreshHighlighting()
    {
        var highlighter = new CodeSyntaxHighlighter();
        foreach (var code in new[] { "/* hello\r\n", "/* hello\r\nworld */\r\nconst x = 1;", "// changed\r\nconst x = 2;", "const x = \"unfinished" })
        {
            var result = highlighter.Highlight("js", code);
            Assert.AreEqual(code, string.Concat(result.Select(token => token.Text)));
            CollectionAssert.AreEqual(new CodeSyntaxHighlighter().Highlight("js", code).ToArray(), result.ToArray());
        }
    }

    [TestMethod]
    public void UnknownAndHugeBlocksStayPlain()
    {
        var highlighter = new CodeSyntaxHighlighter();
        Assert.AreEqual("plain", highlighter.Highlight("unknown", "hello").Single().Kind);
        var code = new string('x', CodeSyntaxHighlighter.MaximumCharacters + 1);
        Assert.AreEqual(code, highlighter.Highlight("cs", code).Single().Text);
    }
}
