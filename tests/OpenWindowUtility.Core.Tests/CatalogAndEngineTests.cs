using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;
using OpenWindowUtility.Core.Packages;
using OpenWindowUtility.Core.Updates;

namespace OpenWindowUtility.Core.Tests;

public sealed class CatalogLoaderTests
{
    [Fact]
    public void EmbeddedCatalogs_LoadAndValidate()
    {
        var catalog = new CatalogLoader().Load();
        Assert.NotEmpty(catalog.Apps.Items);
        Assert.NotEmpty(catalog.Tweaks.Items);
        Assert.NotEmpty(catalog.Features.Items);
        Assert.True(catalog.Presets.Presets.ContainsKey("essential"));
        Assert.Equal(catalog.Apps.Items.Count, catalog.Apps.Items.Select(x => x.Id).Distinct().Count());
        Assert.Contains(catalog.Apps.Items, x => x.Id == "vscodium");
        Assert.Contains(catalog.Apps.Items, x => x.Id == "vlc" && !string.IsNullOrWhiteSpace(x.Icon));
        Assert.Contains(catalog.Apps.Items, x => x.Id == "whatsapp" && !string.IsNullOrWhiteSpace(x.Icon));
        Assert.Contains(catalog.Apps.Items, x => x.Id == "sourcetree" && !string.IsNullOrWhiteSpace(x.Icon));
        Assert.Contains(catalog.Tweaks.Items, x => x.Id == "dns.cloudflare");
        Assert.Contains(catalog.Tweaks.Items, x => x.Id == "power.ultimate-performance");
        Assert.All(catalog.Apps.Items, app =>
        {
            var url = AppIcon.UrlFor(app);
            Assert.False(string.IsNullOrWhiteSpace(url));
            Assert.True(AppIcon.IsTrusted(app, url!));
        });
    }

    [Fact]
    public void AppIcon_UsesGoogleFaviconUnlessOverridden()
    {
        Assert.Equal(
            "https://www.google.com/s2/favicons?sz=128&domain=brave.com",
            AppIcon.UrlFor("https://brave.com"));
        Assert.Null(AppIcon.UrlFor(""));
        Assert.Null(AppIcon.UrlFor("not-a-url"));
        Assert.Null(AppIcon.TryHttps("http://example.com/icon.png"));

        var withIcon = new AppEntry
        {
            Id = "vlc",
            Category = "multimedia",
            Homepage = "https://www.videolan.org/vlc",
            Icon = "https://www.videolan.org/images/VLC-IconSmall.png"
        };
        Assert.Equal(withIcon.Icon, AppIcon.UrlFor(withIcon));
        Assert.True(AppIcon.IsTrusted(withIcon, withIcon.Icon!));
        Assert.False(AppIcon.IsTrusted(withIcon, "https://evil.example/vlc.png"));
        Assert.False(AppIcon.IsTrusted(
            new AppEntry { Id = "brave", Category = "browsers", Homepage = "https://brave.com" },
            "https://com/pwn.png"));
        Assert.Null(AppIcon.TryHttps("https://user:pass@cdn.jsdelivr.net/x.png"));
        Assert.Equal(
            "https://github.com/jesseduffield.png?size=128",
            AppIcon.GitHubOwnerAvatar("https://github.com/jesseduffield/lazygit"));
    }

    [Fact]
    public void EssentialPreset_DoesNotIncludeBitLockerOrOneDrive()
    {
        var catalog = new CatalogLoader().Load();
        var essential = catalog.Presets.Presets["essential"];
        Assert.DoesNotContain("security.bitlocker-disable", essential);
        Assert.DoesNotContain("apps.remove-onedrive", essential);
        Assert.All(essential, id =>
        {
            var tweak = catalog.Tweaks.Items.Single(x => x.Id == id);
            Assert.NotEqual(RiskLevel.Advanced, tweak.Risk);
        });
    }

    [Fact]
    public void VietnameseCatalog_FallsBackButHasKeys()
    {
        var catalog = new CatalogLoader().Load();
        var name = catalog.I18n.Get("vi", "app.brave.name");
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.NotEqual("app.brave.name", name);
    }
}

public sealed class PackageResolverTests
{
    [Fact]
    public void Auto_PrefersWinget()
    {
        var app = new AppEntry { Id = "x", Category = "utilities", Winget = "Vendor.App", Chocolatey = "app" };
        var resolved = PackageResolver.Resolve(app, PackageManagerPreference.Auto);
        Assert.Equal("winget", resolved.Manager);
        Assert.Equal("Vendor.App", resolved.PackageId);
    }

    [Fact]
    public void Chocolatey_FallsBackToWinget()
    {
        var app = new AppEntry { Id = "x", Category = "utilities", Winget = "Vendor.App" };
        var resolved = PackageResolver.Resolve(app, PackageManagerPreference.Chocolatey);
        Assert.Equal("winget", resolved.Manager);
    }

    [Fact]
    public void Winget_FallsBackToChocolatey()
    {
        var app = new AppEntry { Id = "x", Category = "utilities", Chocolatey = "app" };
        var resolved = PackageResolver.Resolve(app, PackageManagerPreference.Winget);
        Assert.Equal("chocolatey", resolved.Manager);
        Assert.Equal("app", resolved.PackageId);
    }
}

public sealed class UpdatePolicyTests
{
    [Fact]
    public void SecurityPolicy_DefersFeatureAndQualityUpdates()
    {
        var engine = new UpdatePolicyEngine(new NoopExecutor(), new Safety.UndoJournal(Path.GetTempFileName()));
        var ops = engine.BuildApply(UpdatePolicyKind.Security).OfType<RegistrySetOperation>().ToList();
        Assert.Contains(ops, x => x.Name == "DeferFeatureUpdatesPeriodInDays" && x.Value.GetInt32() == 365);
        Assert.Contains(ops, x => x.Name == "DeferQualityUpdatesPeriodInDays" && x.Value.GetInt32() == 4);
    }

    [Fact]
    public void DisableAll_StopsUpdateService()
    {
        var engine = new UpdatePolicyEngine(new NoopExecutor(), new Safety.UndoJournal(Path.GetTempFileName()));
        var ops = engine.BuildApply(UpdatePolicyKind.DisableAll);
        Assert.Contains(ops.OfType<ServiceStartupOperation>(), x => x.ServiceName == "wuauserv" && x.StartupType == "disabled");
    }

    private sealed class NoopExecutor : IOperationExecutor
    {
        public Task ExecuteAsync(Operation operation, IJobLog log, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

public sealed class ProcessAllowlistTests
{
    [Theory]
    [InlineData("dism.exe", false, true)]
    [InlineData("sysdm.cpl", true, true)]
    [InlineData("cmd.exe", false, false)]
    [InlineData("powershell.exe", false, true)]
    [InlineData("evil.exe", true, false)]
    public void Allowlist(string file, bool shell, bool expected)
    {
        Assert.Equal(expected, ProcessAllowlist.IsAllowed(file, shell));
    }

    [Fact]
    public void AppxNames_RejectMetacharacters()
    {
        Assert.True(ProcessAllowlist.IsSafeAppxName("Microsoft.Copilot"));
        Assert.False(ProcessAllowlist.IsSafeAppxName("Microsoft'; Remove-Item C:\\"));
        Assert.True(ProcessAllowlist.IsSafePackageId("Notepad++.Notepad++"));
        Assert.False(ProcessAllowlist.IsSafePackageId("foo --manifest x"));
    }
}

public sealed class PackageEngineUpgradeTests
{
    [Fact]
    public async Task UpgradeAll_WingetNothingToUpdate_Succeeds()
    {
        var runner = new StubRunner(-1978335212);
        var engine = new PackageEngine(runner);
        await engine.UpgradeAllAsync(PackageManagerPreference.Winget, new NullJob(), CancellationToken.None);
        Assert.Contains(runner.Calls, x => x.Contains("upgrade --all", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetDetails_ParsesWingetShow()
    {
        var runner = new StubRunner(
            0,
            """
            Found VLC media player [VideoLAN.VLC]
            Version: 3.0.23
            Publisher: VideoLAN
            Publisher Url: https://www.videolan.org/
              Release Date: 2025-12-31
            """);
        var engine = new PackageEngine(runner);
        var details = await engine.GetDetailsAsync("VideoLAN.VLC", CancellationToken.None);
        Assert.Equal("VideoLAN", details.Publisher);
        Assert.Equal("3.0.23", details.Version);
        Assert.Equal("2025-12-31", details.ReleaseDate);
        Assert.Contains(runner.Calls, x => x.Contains("show --id \"VideoLAN.VLC\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetDetails_RejectsUnsafePackageId()
    {
        var runner = new StubRunner(0, "Publisher: Evil");
        var engine = new PackageEngine(runner);
        var details = await engine.GetDetailsAsync("foo --manifest evil.yaml", CancellationToken.None);
        Assert.Equal("foo --manifest evil", details.Publisher);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task GetDetails_UsesPublisherPrefixWhenShowHasNoPublisher()
    {
        var engine = new PackageEngine(new StubRunner(0, "Found something\n"));
        var details = await engine.GetDetailsAsync("VideoLAN.VLC", CancellationToken.None);
        Assert.Equal("VideoLAN", details.Publisher);
    }

    [Fact]
    public void PackageShowParser_PublisherFallbackAndLabels()
    {
        Assert.Equal("VideoLAN", PackageShowParser.PublisherFallback("VideoLAN.VLC"));
        Assert.Equal("—", PackageShowParser.PublisherFallback(null));
        var vi = PackageShowParser.Parse("Nhà phát hành: VideoLAN\nPhiên bản: 3.0.23\nNgày phát hành: 2025-12-31\n");
        Assert.Equal("VideoLAN", vi.Publisher);
        Assert.Equal("3.0.23", vi.Version);
        Assert.Equal("2025-12-31", vi.ReleaseDate);
        var stolen = PackageShowParser.Parse("Publisher Url: https://example\nPublisher: VideoLAN\n");
        Assert.Equal("VideoLAN", stolen.Publisher);
    }

    private sealed class StubRunner(int exitCode, string output = "") : IProcessRunner
    {
        public List<string> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null,
            bool useShellExecute = false)
        {
            Calls.Add(fileName + " " + arguments);
            return Task.FromResult(new ProcessResult { ExitCode = exitCode, StandardOutput = output });
        }
    }

    private sealed class NullJob : IJobContext
    {
        public IProgress<double>? Progress => null;
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
