using System.Text.Json;
using System.Text.RegularExpressions;
using PiAgentGui.Models.Updates;

namespace PiAgentGui.Services.Updates;

public static class GitHubReleaseParser
{
    public const string Repository = "Eliaszac/pi-agent-winui";
    public static AppRelease? Parse(JsonElement json, Version current)
    {
        if (json.GetProperty("draft").GetBoolean() || json.GetProperty("prerelease").GetBoolean()) return null;
        var tag = json.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") || !Version.TryParse(tag.TrimStart('v'), out var version))
            throw new InvalidDataException("The release has an unsupported version number.");
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;
        var file = $"PiDesktop-Setup-{version}-x64.exe";
        var assets = json.GetProperty("assets").EnumerateArray().ToArray();
        var installers = assets.Where(a => a.GetProperty("name").GetString() == file).ToArray();
        var checksums = assets.Where(a => a.GetProperty("name").GetString() == file + ".sha256").ToArray();
        if (installers.Length != 1 || checksums.Length != 1) throw new InvalidDataException("The release installer or checksum is not available yet. Try again later.");
        var installer = installers[0];
        var size = installer.GetProperty("size").GetInt64();
        if (size <= 0 || size > 1024L * 1024 * 1024) throw new InvalidDataException("The release installer size is invalid.");
        Uri Asset(JsonElement asset)
        {
            var name = asset.GetProperty("name").GetString()!;
            var expected = new Uri($"https://github.com/{Repository}/releases/download/{tag}/{name}");
            if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var actual) || actual != expected)
                throw new InvalidDataException("The release asset is not from the expected repository.");
            return actual;
        }
        return new(version, file, Asset(installer), Asset(checksums[0]), size,
            installer.TryGetProperty("digest", out var digest) ? digest.GetString() : null);
    }
}
