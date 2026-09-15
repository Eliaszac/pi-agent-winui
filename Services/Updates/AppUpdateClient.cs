using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using PiAgentGui.Models.Updates;

namespace PiAgentGui.Services.Updates;

public sealed class AppUpdateClient(HttpClient http, string directory)
{
    public async Task<AppRelease?> CheckAsync(Version current, CancellationToken token)
    {
        using var response = await GetAsync(new Uri($"https://api.github.com/repos/{GitHubReleaseParser.Repository}/releases/latest"), token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(2 * 1024 * 1024, token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return GitHubReleaseParser.Parse(json.RootElement, current);
    }

    public async Task<string> DownloadAsync(AppRelease release, IProgress<double> progress, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        CleanOldDownloads(release.FileName);
        using var checksumResponse = await GetAsync(release.Checksum, token);
        checksumResponse.EnsureSuccessStatusCode();
        await checksumResponse.Content.LoadIntoBufferAsync(4096, token);
        var checksumText = await checksumResponse.Content.ReadAsStringAsync(token);
        var match = Regex.Match(checksumText.Trim(), @"^([A-Fa-f0-9]{64})\s+\*?" + Regex.Escape(release.FileName) + "$", RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("The release checksum is invalid.");
        var expected = match.Groups[1].Value;
        if (release.Digest is { Length: > 0 } digest && !digest.Equals("sha256:" + expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GitHub and the release checksum disagree. The update was not downloaded.");
        var path = Path.Combine(directory, release.FileName);
        if (File.Exists(path) && await VerifyAsync(path, release.Size, expected, token)) return path;
        var partial = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var response = await GetAsync(release.Installer, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } size && size != release.Size) throw new InvalidDataException("The installer size does not match the release.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                var lastPercent = -1;
                int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > release.Size) throw new InvalidDataException("The installer exceeds its expected size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    var percent = (int)(total * 100d / release.Size);
                    if (percent != lastPercent) { lastPercent = percent; progress.Report(percent); }
                }
            }
            if (!await VerifyAsync(partial, release.Size, expected, token)) throw new InvalidDataException("The download failed checksum verification. Please download it again.");
            File.Move(partial, path, true);
            return path;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    public static async Task<bool> VerifyAsync(string path, long size, string hash, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return file.Length == size && Convert.ToHexString(await SHA256.HashDataAsync(file, token)).Equals(hash, StringComparison.OrdinalIgnoreCase);
    }

    private void CleanOldDownloads(string keep)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (name == keep || !Regex.IsMatch(name, @"^PiDesktop-Setup-\d+\.\d+\.\d+-x64\.exe(?:\.[a-f0-9]{32}\.partial)?$")) continue;
            if (name.EndsWith(".partial", StringComparison.Ordinal) && File.GetLastWriteTimeUtc(file) > DateTime.UtcNow.AddDays(-1)) continue;
            try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token)
    {
        for (var redirects = 0; redirects < 6; redirects++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0 ||
                uri.Host is not ("api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new InvalidDataException("The update download redirected to an unexpected location.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("PiDesktop-Updater/1.2.0");
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is { } location)
            {
                uri = new Uri(uri, location);
                response.Dispose();
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Too many update download redirects.");
    }
}
