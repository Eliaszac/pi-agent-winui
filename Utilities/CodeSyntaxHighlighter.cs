using System.Diagnostics;
using PiAgentGui.Models.Conversations;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace PiAgentGui.Utilities;

/// <summary>Tokenizes bounded code blocks and reuses unchanged lines during streaming.</summary>
public sealed class CodeSyntaxHighlighter
{
    public const int MaximumCharacters = 64_000;
    private static readonly object Gate = new();
    private static readonly Lazy<RegistryOptions> Options = new(() =>
    {
        var options = new RegistryOptions(ThemeName.DarkPlus);
        options.LoadFromLocalDir(Path.Combine(AppContext.BaseDirectory, "Assets", "Syntax"));
        return options;
    });
    private static readonly Lazy<Registry> Registry = new(() => new Registry(Options.Value));
    private readonly List<(string Text, IStateStack? State, CodeToken[] Tokens)> lines = [];
    private string? previousScope;

    public IReadOnlyList<CodeToken> Highlight(string label, string code, CancellationToken cancellationToken = default)
    {
        if (code.Length > MaximumCharacters) return [new(code, "plain")];
        lock (Gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = ResolveScope(label);
            if (scope is null) return [new(code, "plain")];
            if (scope != previousScope) { lines.Clear(); previousScope = scope; }
            var grammar = Registry.Value.LoadGrammar(scope);
            if (grammar is null) return [new(code, "plain")];
            var result = new List<CodeToken>();
            IStateStack? state = null;
            var watch = Stopwatch.StartNew();
            var offset = 0;
            var index = 0;
            while (offset < code.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var newline = code.IndexOf('\n', offset);
                var end = newline < 0 ? code.Length : newline + 1;
                var line = code[offset..end];
                if (line.Length > 8_000 || index >= 2_000 || watch.ElapsedMilliseconds > 400 || result.Count > 6_000)
                { lines.Clear(); return [new(code, "plain")]; }
                if (index >= lines.Count || lines[index].Text != line)
                {
                    if (index < lines.Count) lines.RemoveRange(index, lines.Count - index);
                    var parsed = grammar.TokenizeLine(line.TrimEnd('\r', '\n'), state, TimeSpan.FromMilliseconds(20));
                    var tokens = new List<CodeToken>();
                    var cursor = 0;
                    foreach (var token in parsed.Tokens)
                    {
                        var start = Math.Clamp(token.StartIndex, cursor, line.Length);
                        var stop = Math.Clamp(token.EndIndex, start, line.Length);
                        if (start > cursor) tokens.Add(new(line[cursor..start], "plain"));
                        if (stop > start) tokens.Add(new(line[start..stop], Kind(token.Scopes)));
                        cursor = stop;
                    }
                    if (cursor < line.Length) tokens.Add(new(line[cursor..], "plain"));
                    lines.Add((line, parsed.RuleStack, tokens.ToArray()));
                }
                result.AddRange(lines[index].Tokens);
                if (result.Count > 6_000) { lines.Clear(); return [new(code, "plain")]; }
                state = lines[index].State;
                index++;
                offset = end;
            }
            if (index < lines.Count) lines.RemoveRange(index, lines.Count - index);
            return result;
        }
    }

    private static string? ResolveScope(string label)
    {
        var name = label.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";
        var id = name switch
        {
            "cs" or "c#" => "csharp", "js" => "javascript", "ts" => "typescript",
            "jsx" => "javascriptreact", "tsx" => "typescriptreact", "py" => "python",
            "sh" or "bash" or "shell" or "zsh" => "shellscript",
            "ps" or "ps1" or "pwsh" => "powershell", "html" or "xml" or "css" or "json" or "java" or "svelte" => name,
            _ => name
        };
        return Options.Value.GetScopeByLanguageId(id) ?? Options.Value.GetScopeByExtension(Path.GetExtension(name));
    }

    private static string Kind(IEnumerable<string> scopes)
    {
        var all = scopes.ToArray();
        if (all.Any(scope => scope.StartsWith("comment"))) return "comment";
        if (all.Any(scope => scope.StartsWith("string"))) return "string";
        if (all.Any(scope => scope.StartsWith("constant.numeric"))) return "number";
        if (all.Any(scope => scope.StartsWith("keyword") || scope.StartsWith("storage") || scope.StartsWith("constant.language"))) return "keyword";
        if (all.Any(scope => scope.StartsWith("entity.name.tag"))) return "keyword";
        if (all.Any(scope => scope.StartsWith("entity.name.function") || scope.StartsWith("support.function"))) return "function";
        if (all.Any(scope => scope.StartsWith("entity.name.type") || scope.StartsWith("support.type") || scope.StartsWith("support.class"))) return "type";
        if (all.Any(scope => scope.StartsWith("entity.other.attribute-name") || scope.StartsWith("variable"))) return "variable";
        return "plain";
    }
}
