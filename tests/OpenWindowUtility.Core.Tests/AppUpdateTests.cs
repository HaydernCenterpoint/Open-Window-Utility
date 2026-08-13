using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using OpenWindowUtility.Core.AppUpdate;

namespace OpenWindowUtility.Core.Tests;

public sealed class AppUpdateTests
{
    [Fact]
    public void IsNewer_ComparesVersions()
    {
        Assert.True(AppUpdateClient.IsNewer("1.0.0", "1.0.1"));
        Assert.True(AppUpdateClient.IsNewer("1.0.0", "v1.1.0"));
        Assert.False(AppUpdateClient.IsNewer("1.0.1", "1.0.1"));
        Assert.False(AppUpdateClient.IsNewer("1.1.0", "1.0.9"));
    }

    [Fact]
    public void ParseManifest_RejectsHttpAndBadHash()
    {
        var feed = new Uri("https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/latest/download/latest.json");
        var sha = new string('a', 64);
        var ok = AppUpdateClient.ParseManifest(
            $$"""{"version":"1.0.1","url":"https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/download/v1.0.1/OpenWindowUtility.exe","sha256":"{{sha}}"}""",
            feed);
        Assert.Equal("1.0.1", ok.Version);

        Assert.Throws<InvalidOperationException>(() => AppUpdateClient.ParseManifest(
            $$"""{"version":"1.0.1","url":"http://evil.example/owu.exe","sha256":"{{sha}}"}""",
            feed));
        Assert.Throws<InvalidOperationException>(() => AppUpdateClient.ParseManifest(
            """{"version":"1.0.1","url":"https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/download/v1.0.1/OpenWindowUtility.exe","sha256":"abc"}""",
            feed));
        Assert.Throws<InvalidOperationException>(() => AppUpdateClient.ParseManifest(
            $$"""{"version":"1.0.1","url":"https://user:pass@github.com/x.exe","sha256":"{{sha}}"}""",
            feed));
    }

    [Fact]
    public void HostAllowlist_AllowsGitHubAndFeedHost()
    {
        var feed = new Uri("https://example.com/latest.json");
        Assert.True(AppUpdateClient.IsAllowedHost(new Uri("https://example.com/OpenWindowUtility.exe"), feed));
        Assert.True(AppUpdateClient.IsAllowedHost(new Uri("https://objects.githubusercontent.com/a.exe"), feed));
        Assert.False(AppUpdateClient.IsAllowedHost(new Uri("https://evil.example/a.exe"), feed));
        Assert.False(AppUpdateClient.IsAllowedHost(new Uri("http://example.com/a.exe"), feed));
        Assert.Null(AppUpdateClient.ParseHttpsUri("https://user:pass@example.com/x"));
    }

    [Fact]
    public void TryGitHubRepo_StripsGitSuffix()
    {
        Assert.True(AppUpdateClient.TryGitHubRepo(
            new Uri("https://github.com/HaydernCenterpoint/Open-Window-Utility.git"),
            out var owner,
            out var repo));
        Assert.Equal("HaydernCenterpoint", owner);
        Assert.Equal("Open-Window-Utility", repo);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNullWhenCurrent()
    {
        var sha = new string('b', 64);
        var json = $$"""{"version":"1.0.0","url":"https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/download/v1.0.0/OpenWindowUtility.exe","sha256":"{{sha}}"}""";
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = new AppUpdateClient(handler, "1.0.0");
        var found = await client.CheckAsync(AppUpdateClient.DefaultFeedUrl, CancellationToken.None);
        Assert.Null(found);
    }

    [Fact]
    public async Task CheckAndDownload_VerifiesSha256()
    {
        var payload = Encoding.UTF8.GetBytes("owu-exe");
        var sha = Convert.ToHexString(SHA256.HashData(payload));
        var json = $$"""{"version":"1.2.0","url":"https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/download/v1.2.0/OpenWindowUtility.exe","sha256":"{{sha}}"}""";
        var handler = new StubHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("latest.json", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        });
        var client = new AppUpdateClient(handler, "1.0.0");
        var release = await client.CheckAsync(AppUpdateClient.DefaultFeedUrl, CancellationToken.None);
        Assert.NotNull(release);
        Assert.Equal("1.2.0", release!.Version);
        var path = await client.DownloadAsync(release, new Uri(AppUpdateClient.DefaultFeedUrl), CancellationToken.None);
        Assert.True(File.Exists(path));
        Assert.Equal(payload, File.ReadAllBytes(path));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
