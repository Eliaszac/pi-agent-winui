namespace PiAgentGui.Utilities;

/// <summary>Retains only a bounded in-memory tail and returns fixed, non-sensitive startup hints.</summary>
public sealed class PiExitHint
{
    private readonly object gate = new();
    private string tail = "";
    public void Append(ReadOnlySpan<char> text)
    {
        lock (gate) { tail += text.ToString(); if (tail.Length > 8192) tail = tail[^8192..]; }
    }
    public string Describe()
    {
        lock (gate)
        {
            var text = tail.ToLowerInvariant();
            if (text.Contains("cannot find module") || text.Contains("module_not_found") || text.Contains("package path not exported") || text.Contains("err_package_path_not_exported"))
                return "Pi reported a missing or incompatible dependency. Check installed extensions and their setup requirements.";
            if (text.Contains("failed to load extension") || text.Contains("error loading extension"))
                return "Pi reported an extension loading failure. Check installed extensions before reconnecting.";
            if (text.Contains("eacces") || text.Contains("eperm")) return "Pi reported a file access error. Check access to the project and Pi storage folders.";
            if (text.Contains("heap out of memory")) return "Pi ran out of memory. Close unused conversations before reconnecting.";
            return "Reconnect to reopen the saved session. No requests will be resent automatically.";
        }
    }
}
