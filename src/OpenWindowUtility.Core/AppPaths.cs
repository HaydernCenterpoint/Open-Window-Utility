namespace OpenWindowUtility.Core;

public static class AppPaths
{
    public const string ProductName = "OpenWindowUtility";
    public const string DisplayName = "Open Window Utility";
    public static string Version =>
        typeof(AppPaths).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);

    public static string Logs => Path.Combine(Root, "logs");
    public static string JournalFile => Path.Combine(Root, "undo-journal.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string Updates => Path.Combine(Root, "updates");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Updates);
    }
}
