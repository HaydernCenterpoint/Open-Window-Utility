namespace OpenWindowUtility.Core;

public static class AppPaths
{
    public const string ProductName = "OpenWindowUtility";
    public const string DisplayName = "Open Window Utility";
    public const string DataFolderName = "data";

    private static string? _overrideRoot;
    private static string? _resolvedRoot;

    public static string Version =>
        typeof(AppPaths).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string Root => _overrideRoot ?? (_resolvedRoot ??= ResolveRoot());

    public static string Logs => Path.Combine(Root, "logs");
    public static string JournalFile => Path.Combine(Root, "undo-journal.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string Updates => Path.Combine(Root, "updates");

    public static void OverrideRoot(string? path)
    {
        _overrideRoot = path;
        _resolvedRoot = null;
    }

    public static string ChooseRoot(string? exeDirectory, string appDataDirectory, Func<string, bool>? canUse = null)
    {
        var check = canUse ?? TryUse;
        if (!string.IsNullOrWhiteSpace(exeDirectory))
        {
            var portable = Path.Combine(exeDirectory, DataFolderName);
            if (check(portable))
            {
                return portable;
            }
        }

        return Path.Combine(appDataDirectory, ProductName);
    }

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Updates);
    }

    private static string ResolveRoot()
    {
        return ChooseRoot(
            Path.GetDirectoryName(Environment.ProcessPath),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    private static bool TryUse(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write");
            using (File.Open(probe, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
