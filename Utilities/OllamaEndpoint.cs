using System.Security.Cryptography;
using System.Text;

namespace PiAgentGui.Utilities;

/// <summary>Normalizes an Ollama server root without accepting credentials or request fragments.</summary>
public static class OllamaEndpoint
{
    public const string Local = "http://localhost:11434";
    public static Uri Parse(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException("Enter an HTTP or HTTPS server address without credentials, a query or a fragment.");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/v1", StringComparison.Ordinal)) path = path[..^3];
        return new UriBuilder(uri) { Path = path + "/" }.Uri;
    }
    public static string ProviderId(Uri endpoint) => endpoint == Parse(Local) ? "ollama"
        : "ollama-remote-" + new string(endpoint.Host.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.').ToArray())
          + "-" + endpoint.Port + "-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.AbsoluteUri)))[..8];
}
