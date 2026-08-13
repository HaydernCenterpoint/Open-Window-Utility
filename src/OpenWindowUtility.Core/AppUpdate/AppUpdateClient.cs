using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenWindowUtility.Core.AppUpdate;

public sealed class AppUpdateClient
{
    public const string DefaultFeedUrl =
        "https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/latest/download/latest.json";

    public const long MaxDownloadBytes = 200L * 1024 * 1024;

    private static readonly Regex GitHubRepo = new(
        @"^https://(?:www\.)?github\.com/(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+)(?:/|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] GitHubHosts =
    [
        "github.com",
        "www.github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "github-releases.githubusercontent.com"
    ];

    private readonly HttpClient _http;
    private readonly string _currentVersion;

    public AppUpdateClient(HttpMessageHandler? handler = null, string? currentVersion = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromMinutes(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"OpenWindowUtility/{AppPaths.Version}");
        _currentVersion = currentVersion ?? AppPaths.Version;
    }

    public string CurrentVersion => _currentVersion;

    public async Task<AppRelease?> CheckAsync(string feedUrl, CancellationToken cancellationToken)
    {
        var feed = ParseHttpsUri(feedUrl) ?? throw new InvalidOperationException("Update feed must be an HTTPS URL.");
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(feed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException("Could not reach the update feed.", ex);
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound
                && TryGitHubRepo(feed, out var owner, out var repo))
            {
                return await CheckGitHubApiAsync(owner, repo, cancellationToken).ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var release = ParseManifest(json, feed);
            return IsNewer(_currentVersion, release.Version) ? release : null;
        }
    }

    public async Task<string> DownloadAsync(AppRelease release, Uri feedUri, CancellationToken cancellationToken)
    {
        var uri = ParseHttpsUri(release.Url) ?? throw new InvalidOperationException("Update package URL is not HTTPS.");
        if (!IsAllowedHost(uri, feedUri))
        {
            throw new InvalidOperationException($"Update host '{uri.Host}' is not allowed.");
        }

        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri is { } final && !IsAllowedHost(final, feedUri))
        {
            throw new InvalidOperationException($"Update redirect host '{final.Host}' is not allowed.");
        }

        if (response.Content.Headers.ContentLength is { } length && length > MaxDownloadBytes)
        {
            throw new InvalidOperationException("Update package is larger than 200 MB.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var copy = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(copy, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxDownloadBytes)
            {
                throw new InvalidOperationException("Update package is larger than 200 MB.");
            }

            buffer.Write(copy, 0, read);
        }

        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update SHA-256 did not match the feed.");
        }

        var dir = Path.Combine(AppPaths.Updates, release.Version);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "OpenWindowUtility.exe");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return path;
    }

    public static bool IsNewer(string current, string remote)
    {
        if (!TryParseVersion(current, out var left) || !TryParseVersion(remote, out var right))
        {
            return false;
        }

        return right > left;
    }

    public static AppRelease ParseManifest(string json, Uri feedUri)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var version = root.GetProperty("version").GetString() ?? "";
        var url = root.GetProperty("url").GetString() ?? "";
        var sha = root.TryGetProperty("sha256", out var shaEl) ? shaEl.GetString() ?? "" : "";
        return Validate(version, url, sha, feedUri);
    }

    public static bool TryParseVersion(string text, out Version version)
    {
        var trimmed = text.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out version!);
    }

    public static Uri? ParseHttpsUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return uri;
    }

    public static bool IsAllowedHost(Uri uri, Uri feedUri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (uri.Host.Equals(feedUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var host in GitHubHosts)
        {
            if (uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGitHubRepo(Uri feed, out string owner, out string repo)
    {
        var match = GitHubRepo.Match(feed.AbsoluteUri);
        if (!match.Success)
        {
            owner = "";
            repo = "";
            return false;
        }

        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value;
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repo = repo[..^4];
        }
        return owner.Length > 0 && repo.Length > 0;
    }

    private async Task<AppRelease?> CheckGitHubApiAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var api = new Uri($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
        using var request = new HttpRequestMessage(HttpMethod.Get, api);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            if (!name.Equals("OpenWindowUtility.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = asset.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            var digest = asset.TryGetProperty("digest", out var digestEl) ? digestEl.GetString() ?? "" : "";
            var sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? digest["sha256:".Length..]
                : "";
            var release = Validate(tag, url, sha, api);
            return IsNewer(_currentVersion, release.Version) ? release : null;
        }

        return null;
    }

    private static AppRelease Validate(string version, string url, string sha256, Uri feedUri)
    {
        if (!TryParseVersion(version, out _))
        {
            throw new InvalidOperationException("Update feed has an invalid version.");
        }

        var uri = ParseHttpsUri(url) ?? throw new InvalidOperationException("Update package URL must be HTTPS.");
        if (!IsAllowedHost(uri, feedUri))
        {
            throw new InvalidOperationException($"Update host '{uri.Host}' is not allowed.");
        }

        if (sha256.Length != 64 || !sha256.All(IsHex))
        {
            throw new InvalidOperationException("Update feed must include a 64-character SHA-256.");
        }

        return new AppRelease
        {
            Version = version.Trim().TrimStart('v', 'V'),
            Url = uri.AbsoluteUri,
            Sha256 = sha256
        };
    }

    private static bool IsHex(char ch) =>
        ch is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
