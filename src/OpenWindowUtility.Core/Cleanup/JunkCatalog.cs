namespace OpenWindowUtility.Core.Cleanup;

public static class JunkCatalog
{
    public static readonly string[] GroupOrder = ["system", "browser", "ai", "dev", "deep"];

    public static IReadOnlyList<JunkCategoryInfo> Categories { get; } =
    [
        Cat(JunkKind.UserTemp, "user-temp", "system", deep: false),
        Cat(JunkKind.WindowsTemp, "windows-temp", "system", deep: false),
        Cat(JunkKind.RecycleBin, "recycle", "system", deep: false),
        Cat(JunkKind.ThumbnailCache, "thumbnails", "system", deep: false),
        Cat(JunkKind.DeliveryOptimization, "delivery", "system", deep: false),
        Cat(JunkKind.WindowsUpdateCache, "wu-cache", "system", deep: false),
        Cat(JunkKind.ErrorReports, "wer", "system", deep: false),
        Cat(JunkKind.CrashDumps, "dumps", "system", deep: false),
        Cat(JunkKind.FontCache, "font-cache", "system", deep: false),
        Cat(JunkKind.ExplorerCache, "explorer-cache", "system", deep: false),
        Cat(JunkKind.DirectXShaderCache, "dx-cache", "system", deep: false),
        Cat(JunkKind.BrowserCache, "browser", "browser", deep: true),
        Cat(JunkKind.AiAppCache, "ai-app", "ai", deep: true),
        Cat(JunkKind.AiModelCache, "ai-model", "ai", deep: true),
        Cat(JunkKind.DevPackageCache, "dev-cache", "dev", deep: true),
        Cat(JunkKind.Prefetch, "prefetch", "deep", deep: true),
        Cat(JunkKind.WindowsOld, "windows-old", "deep", deep: true)
    ];

    public static IReadOnlyList<JunkRoot> RootsFor(JunkKind kind, CleanupEnvironment env)
    {
        return kind switch
        {
            JunkKind.UserTemp => UserTemp(env),
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
            JunkKind.FontCache => FontCaches(env),
            JunkKind.ExplorerCache => ExplorerCaches(env),
            JunkKind.DirectXShaderCache => ShaderCaches(env),
            JunkKind.BrowserCache => BrowserCaches(env),
            JunkKind.AiAppCache => AiAppCaches(env),
            JunkKind.AiModelCache => AiModelCaches(env),
            JunkKind.DevPackageCache => DevPackageCaches(env),
            JunkKind.Prefetch => [Dir(Path.Combine(env.Windows, "Prefetch"), recurse: false, patterns: ["*.pf"])],
            JunkKind.WindowsOld => [Dir(Path.Combine(env.SystemDrive, "Windows.old"))],
            _ => throw new InvalidOperationException($"Unknown junk kind '{kind}'.")
        };
    }

    private static JunkCategoryInfo Cat(JunkKind kind, string id, string group, bool deep) =>
        new() { Kind = kind, Id = id, Group = group, Deep = deep };

    private static List<JunkRoot> UserTemp(CleanupEnvironment env)
    {
        var list = UserLocal(env, @"AppData\Local\Temp");
        list.AddRange(UserLocal(env, @"AppData\LocalLow\Temp"));
        return list;
    }

    private static List<JunkRoot> FontCaches(CleanupEnvironment env)
    {
        var list = UserLocal(env, @"AppData\Local\Microsoft\FontCache");
        list.Add(Dir(Path.Combine(
            env.Windows,
            @"ServiceProfiles\LocalService\AppData\Local\FontCache")));
        return list;
    }

    private static List<JunkRoot> ExplorerCaches(CleanupEnvironment env)
    {
        var list = UserLocal(env, @"AppData\Local\Microsoft\Windows\Caches");
        list.AddRange(UserLocal(env, @"AppData\Local\Microsoft\Windows\Explorer\ThumbCacheToDelete"));
        foreach (var profile in env.UserProfiles)
        {
            list.Add(FileRoot(Path.Combine(profile, @"AppData\Local\IconCache.db")));
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
            AddChromiumUserData(list, Path.Combine(local, @"Google\Chrome SxS\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Microsoft\Edge\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Microsoft\Edge Beta\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Microsoft\Edge Dev\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Chromium\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Vivaldi\User Data"));
            AddChromiumUserData(list, Path.Combine(local, @"Arc\User Data"));
            AddOpera(list, Path.Combine(local, @"Opera Software\Opera Stable"));
            AddOpera(list, Path.Combine(local, @"Opera Software\Opera GX Stable"));
            AddFirefox(list, Path.Combine(local, @"Mozilla\Firefox\Profiles"));
            list.Add(Dir(Path.Combine(local, @"Microsoft\Windows\INetCache")));
        }

        return list;
    }

    private static List<JunkRoot> AiAppCaches(CleanupEnvironment env)
    {
        var list = new List<JunkRoot>();
        string[] electronApps =
        [
            "Cursor",
            "Code",
            "Code - Insiders",
            "VSCodium",
            "Claude",
            "ChatGPT",
            "Windsurf",
            "Trae",
            "Antigravity",
            "Continue"
        ];
        foreach (var profile in env.UserProfiles)
        {
            var roaming = Path.Combine(profile, "AppData", "Roaming");
            var local = Path.Combine(profile, "AppData", "Local");
            foreach (var app in electronApps)
            {
                AddElectronCaches(list, Path.Combine(roaming, app));
                AddElectronCaches(list, Path.Combine(local, app));
            }

            AddElectronCaches(list, Path.Combine(local, "OpenAI"));
            AddElectronCaches(list, Path.Combine(local, "AnthropicClaude"));
            list.Add(Dir(Path.Combine(local, "github-copilot")));
            list.Add(Dir(Path.Combine(local, "GitHubCopilot")));
            list.Add(Dir(Path.Combine(local, "cursor-updater")));
            list.Add(Dir(Path.Combine(local, @"NVIDIA\ComputeCache")));
            list.Add(Dir(Path.Combine(profile, @".nv\ComputeCache")));
            list.Add(Dir(Path.Combine(profile, @".cursor\ai-tracking")));
            list.Add(Dir(Path.Combine(profile, @".ollama\logs")));
            AddPackageCaches(list, Path.Combine(local, "Packages"), "Microsoft.Copilot");
            AddPackageCaches(list, Path.Combine(local, "Packages"), "Microsoft.Windows.Ai");
        }

        return list;
    }

    private static List<JunkRoot> AiModelCaches(CleanupEnvironment env)
    {
        string[] relatives =
        [
            @".cache\huggingface",
            @".cache\torch",
            @".cache\torch_extensions",
            @".cache\lm-studio",
            @".cache\whisper",
            @".cache\ultralytics",
            @".cache\modelscope",
            @".cache\vllm",
            @".triton\cache"
        ];
        var list = new List<JunkRoot>();
        foreach (var relative in relatives)
        {
            list.AddRange(UserLocal(env, relative));
        }

        return list;
    }

    private static List<JunkRoot> DevPackageCaches(CleanupEnvironment env)
    {
        string[] relatives =
        [
            @"AppData\Local\npm-cache",
            @"AppData\Roaming\npm-cache",
            @"AppData\Local\Yarn\Cache",
            @"AppData\Local\pnpm-cache",
            @"AppData\Local\NuGet\v3-cache",
            @"AppData\Local\NuGet\http-cache",
            @"AppData\Local\pip\Cache",
            @"AppData\Local\uv\cache",
            @"AppData\Local\Temp\WinGet",
            @"AppData\Local\Microsoft\WinGet\Cache"
        ];
        var list = new List<JunkRoot>();
        foreach (var relative in relatives)
        {
            list.AddRange(UserLocal(env, relative));
        }

        list.Add(Dir(Path.Combine(env.ProgramData, @"chocolatey\cache")));
        list.Add(Dir(Path.Combine(env.ProgramData, @"chocolatey\lib-bad")));
        return list;
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

    private static void AddElectronCaches(List<JunkRoot> list, string appRoot)
    {
        string[] folders =
        [
            "Cache",
            "CachedData",
            "Code Cache",
            "GPUCache",
            "DawnCache",
            "ShaderCache",
            "GrShaderCache",
            "logs",
            @"Service Worker\CacheStorage"
        ];
        foreach (var folder in folders)
        {
            list.Add(Dir(Path.Combine(appRoot, folder)));
        }
    }

    private static void AddPackageCaches(List<JunkRoot> list, string packagesRoot, string prefix)
    {
        if (!Directory.Exists(packagesRoot))
        {
            return;
        }

        foreach (var dir in SafeDirectories(packagesRoot))
        {
            if (!Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add(Dir(Path.Combine(dir, "LocalCache")));
            list.Add(Dir(Path.Combine(dir, @"AC\INetCache")));
            list.Add(Dir(Path.Combine(dir, @"AC\Temp")));
        }
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
