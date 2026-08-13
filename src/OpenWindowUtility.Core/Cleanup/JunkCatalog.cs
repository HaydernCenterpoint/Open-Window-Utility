namespace OpenWindowUtility.Core.Cleanup;

public static class JunkCatalog
{
    public static IReadOnlyList<JunkCategoryInfo> Categories { get; } =
    [
        new() { Kind = JunkKind.UserTemp, Id = "user-temp", Deep = false },
        new() { Kind = JunkKind.WindowsTemp, Id = "windows-temp", Deep = false },
        new() { Kind = JunkKind.RecycleBin, Id = "recycle", Deep = false },
        new() { Kind = JunkKind.ThumbnailCache, Id = "thumbnails", Deep = false },
        new() { Kind = JunkKind.DeliveryOptimization, Id = "delivery", Deep = false },
        new() { Kind = JunkKind.WindowsUpdateCache, Id = "wu-cache", Deep = false },
        new() { Kind = JunkKind.ErrorReports, Id = "wer", Deep = false },
        new() { Kind = JunkKind.CrashDumps, Id = "dumps", Deep = false },
        new() { Kind = JunkKind.DirectXShaderCache, Id = "dx-cache", Deep = false },
        new() { Kind = JunkKind.BrowserCache, Id = "browser", Deep = true },
        new() { Kind = JunkKind.Prefetch, Id = "prefetch", Deep = true },
        new() { Kind = JunkKind.WindowsOld, Id = "windows-old", Deep = true }
    ];

    public static IReadOnlyList<JunkRoot> RootsFor(JunkKind kind, CleanupEnvironment env)
    {
        return kind switch
        {
            JunkKind.UserTemp => UserLocal(env, @"AppData\Local\Temp"),
            JunkKind.WindowsTemp => [Dir(Path.Combine(env.Windows, "Temp"))],
            JunkKind.RecycleBin => [],
            JunkKind.ThumbnailCache => UserLocal(
                env,
                @"AppData\Local\Microsoft\Windows\Explorer",
                recurse: false,
                patterns: ["thumbcache_*.db", "iconcache*.db"]),
            JunkKind.DeliveryOptimization =>
            [
                Dir(Path.Combine(
                    env.Windows,
                    @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache")),
                Dir(Path.Combine(env.ProgramData, @"Microsoft\Windows\DeliveryOptimization\Cache"))
            ],
            JunkKind.WindowsUpdateCache => [Dir(Path.Combine(env.Windows, @"SoftwareDistribution\Download"))],
            JunkKind.ErrorReports => UserLocal(env, @"AppData\Local\Microsoft\Windows\WER")
                .Concat([Dir(Path.Combine(env.ProgramData, @"Microsoft\Windows\WER"))])
                .ToList(),
            JunkKind.CrashDumps => UserLocal(env, @"AppData\Local\CrashDumps")
                .Concat(
                [
                    Dir(Path.Combine(env.Windows, "Minidump")),
                    FileRoot(Path.Combine(env.Windows, "MEMORY.DMP"))
                ])
                .ToList(),
            JunkKind.DirectXShaderCache => ShaderCaches(env),
            JunkKind.BrowserCache => BrowserCaches(env),
            JunkKind.Prefetch => [Dir(Path.Combine(env.Windows, "Prefetch"), recurse: false, patterns: ["*.pf"])],
            JunkKind.WindowsOld => [Dir(Path.Combine(env.SystemDrive, "Windows.old"))],
            _ => throw new InvalidOperationException($"Unknown junk kind '{kind}'.")
        };
    }

    private static List<JunkRoot> UserLocal(
        CleanupEnvironment env,
        string relative,
        bool recurse = true,
        string[]? patterns = null)
    {
        var list = new List<JunkRoot>();
        foreach (var profile in env.UserProfiles)
        {
            list.Add(new JunkRoot
            {
                Path = Path.Combine(profile, relative),
                Recurse = recurse,
                Patterns = patterns ?? ["*"]
            });
        }

        return list;
    }

    private static List<JunkRoot> ShaderCaches(CleanupEnvironment env)
    {
        string[] relatives =
        [
            @"AppData\Local\D3DSCache",
            @"AppData\Local\NVIDIA\DXCache",
            @"AppData\Local\NVIDIA\GLCache",
            @"AppData\Local\AMD\DxCache",
            @"AppData\Local\AMD\GLCache",
            @"AppData\Local\Intel\ShaderCache"
        ];
        var list = new List<JunkRoot>();
        foreach (var relative in relatives)
        {
            list.AddRange(UserLocal(env, relative));
        }

        return list;
    }

    private static List<JunkRoot> BrowserCaches(CleanupEnvironment env)
    {
        var list = new List<JunkRoot>();
        foreach (var profile in env.UserProfiles)
        {
            var local = Path.Combine(profile, "AppData", "Local");
            AddChromiumUserData(list, Path.Combine(local, @"Google\Chrome\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Microsoft\Edge\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Chromium\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Vivaldi\User Data"));
            AddOpera(list, Path.Combine(local, @"Opera Software\Opera Stable"));
            AddFirefox(list, Path.Combine(local, @"Mozilla\Firefox\Profiles"));
            list.Add(Dir(Path.Combine(local, @"Microsoft\Windows\INetCache")));
        }

        return list;
    }

    private static void AddChromiumUserData(List<JunkRoot> list, string userData)
    {
        if (!Directory.Exists(userData))
        {
            return;
        }

        foreach (var profileDir in SafeDirectories(userData))
        {
            var name = Path.GetFileName(profileDir);
            if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddChromiumCacheFolders(list, profileDir);
        }
    }

    private static void AddOpera(List<JunkRoot> list, string operaStable)
    {
        if (Directory.Exists(operaStable))
        {
            AddChromiumCacheFolders(list, operaStable);
        }
    }

    private static void AddChromiumCacheFolders(List<JunkRoot> list, string profileDir)
    {
        string[] folders =
        [
            "Cache",
            "Code Cache",
            "GPUCache",
            "ShaderCache",
            "GrShaderCache",
            "DawnCache",
            @"Service Worker\CacheStorage"
        ];
        foreach (var folder in folders)
        {
            list.Add(Dir(Path.Combine(profileDir, folder)));
        }
    }

    private static void AddFirefox(List<JunkRoot> list, string profilesRoot)
    {
        if (!Directory.Exists(profilesRoot))
        {
            return;
        }

        foreach (var profileDir in SafeDirectories(profilesRoot))
        {
            list.Add(Dir(Path.Combine(profileDir, "cache2")));
            list.Add(Dir(Path.Combine(profileDir, "startupCache")));
        }
    }

    private static IEnumerable<string> SafeDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root).Where(dir => !CleanupEnvironment.IsReparsePoint(dir));
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static JunkRoot Dir(string path, bool recurse = true, string[]? patterns = null) =>
        new() { Path = path, Recurse = recurse, Patterns = patterns ?? ["*"] };

    private static JunkRoot FileRoot(string path) =>
        new() { Path = path, Recurse = false, Patterns = ["*"] };
}
