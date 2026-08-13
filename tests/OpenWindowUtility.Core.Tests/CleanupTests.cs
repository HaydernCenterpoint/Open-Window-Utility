using OpenWindowUtility.Core.Cleanup;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Tests;

public sealed class CleanupTests
{
    [Fact]
    public void FormatBytes_UsesBinaryUnits()
    {
        Assert.Equal("0 B", CleanupFormatter.FormatBytes(0));
        Assert.Equal("512 B", CleanupFormatter.FormatBytes(512));
        Assert.Equal("1.0 KB", CleanupFormatter.FormatBytes(1024));
        Assert.Equal("1.5 KB", CleanupFormatter.FormatBytes(1536));
        Assert.Equal("1.0 GB", CleanupFormatter.FormatBytes(1L << 30));
    }

    [Fact]
    public void PathGuard_AllowsTempChild_RejectsSystem32Traversal()
    {
        using var fx = CleanupFixture.Create();
        var tempFile = Path.Combine(fx.UserTemp, "a.tmp");
        File.WriteAllText(tempFile, "junk");
        Assert.True(JunkPathGuard.CanDelete(tempFile, JunkKind.UserTemp, fx.Env));
        Assert.False(JunkPathGuard.CanDelete(fx.UserTemp, JunkKind.UserTemp, fx.Env));

        var escaped = Path.GetFullPath(Path.Combine(fx.WindowsTemp, "..", "System32", "keep.dll"));
        Assert.Equal(fx.ProtectedFile, escaped, StringComparer.OrdinalIgnoreCase);
        Assert.False(JunkPathGuard.CanDelete(escaped, JunkKind.WindowsTemp, fx.Env));
        Assert.False(JunkPathGuard.CanDelete(fx.ProtectedFile, JunkKind.UserTemp, fx.Env));
    }

    [Fact]
    public async Task ScanAndClean_ClassifiesAndDeletesOnlyJunk()
    {
        using var fx = CleanupFixture.Create();
        File.WriteAllBytes(Path.Combine(fx.UserTemp, "cache.bin"), new byte[2048]);
        File.WriteAllText(Path.Combine(fx.WindowsTemp, "setup.log"), "tmp");
        Directory.CreateDirectory(fx.ChromeCache);
        File.WriteAllBytes(Path.Combine(fx.ChromeCache, "f_0001"), new byte[4096]);
        File.WriteAllText(fx.ProtectedFile, "do-not-delete");
        fx.Recycle.Bytes = 3000;
        fx.Recycle.Count = 2;

        var engine = new CleanupEngine(fx.Env, fx.Recycle);
        var hits = await engine.ScanAsync(new NullLog(), CancellationToken.None);
        var userTemp = hits.Single(x => x.Kind == JunkKind.UserTemp);
        var browser = hits.Single(x => x.Kind == JunkKind.BrowserCache);
        var recycle = hits.Single(x => x.Kind == JunkKind.RecycleBin);
        Assert.True(userTemp.FileCount >= 1);
        Assert.True(userTemp.Bytes >= 2048);
        Assert.True(browser.FileCount >= 1);
        Assert.Equal(2, recycle.FileCount);
        Assert.Equal(3000, recycle.Bytes);

        await engine.CleanAsync([JunkKind.UserTemp, JunkKind.RecycleBin], new NullLog(), CancellationToken.None);
        Assert.False(File.Exists(Path.Combine(fx.UserTemp, "cache.bin")));
        Assert.True(File.Exists(fx.ProtectedFile));
        Assert.True(File.Exists(Path.Combine(fx.ChromeCache, "f_0001")));
        Assert.True(fx.Recycle.Emptied);
        Assert.Equal(0, fx.Recycle.Count);
    }

    [Fact]
    public void Catalog_HasUniqueIdsAndExhaustiveRoots()
    {
        Assert.Equal(JunkCatalog.Categories.Count, JunkCatalog.Categories.Select(x => x.Id).Distinct().Count());
        Assert.Equal(JunkCatalog.Categories.Count, JunkCatalog.Categories.Select(x => x.Kind).Distinct().Count());
        using var fx = CleanupFixture.Create();
        foreach (var category in JunkCatalog.Categories)
        {
            var roots = JunkCatalog.RootsFor(category.Kind, fx.Env);
            if (category.Kind == JunkKind.RecycleBin)
            {
                Assert.Empty(roots);
            }
            else
            {
                Assert.NotEmpty(roots);
            }
        }
    }

    private sealed class NullLog : IJobLog
    {
        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }

    private sealed class FakeRecycle : IRecycleBinQuery
    {
        public long Bytes;
        public int Count;
        public bool Emptied;

        public (long Bytes, int Count) Query() => (Bytes, Count);

        public void Empty()
        {
            Emptied = true;
            Bytes = 0;
            Count = 0;
        }
    }

    private sealed class CleanupFixture : IDisposable
    {
        public required string Root { get; init; }
        public required CleanupEnvironment Env { get; init; }
        public required string UserTemp { get; init; }
        public required string WindowsTemp { get; init; }
        public required string ChromeCache { get; init; }
        public required string ProtectedFile { get; init; }
        public required FakeRecycle Recycle { get; init; }

        public static CleanupFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "owu-cleanup-" + Guid.NewGuid().ToString("N"));
            var windows = Path.Combine(root, "Windows");
            var programData = Path.Combine(root, "ProgramData");
            var profile = Path.Combine(root, "Users", "Test");
            var userTemp = Path.Combine(profile, "AppData", "Local", "Temp");
            var windowsTemp = Path.Combine(windows, "Temp");
            var chromeCache = Path.Combine(profile, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cache");
            var system32 = Path.Combine(windows, "System32");
            Directory.CreateDirectory(userTemp);
            Directory.CreateDirectory(windowsTemp);
            Directory.CreateDirectory(chromeCache);
            Directory.CreateDirectory(system32);
            Directory.CreateDirectory(programData);
            var protectedFile = Path.Combine(system32, "keep.dll");
            File.WriteAllText(protectedFile, "keep");
            return new CleanupFixture
            {
                Root = root,
                Env = new CleanupEnvironment
                {
                    Windows = windows,
                    ProgramData = programData,
                    SystemDrive = root,
                    UserProfiles = [profile]
                },
                UserTemp = userTemp,
                WindowsTemp = windowsTemp,
                ChromeCache = chromeCache,
                ProtectedFile = protectedFile,
                Recycle = new FakeRecycle()
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (Exception)
            {
                // Fixture cleanup is best-effort.
            }
        }
    }
}
