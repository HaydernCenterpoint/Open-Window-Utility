using System.Text.Json;
using OpenWindowUtility.Core.AppUpdate;

namespace OpenWindowUtility.Core.Settings;

public sealed class AppSettings
{
    public string Language { get; set; } = "auto";
    public bool CreateRestorePoint { get; set; } = true;
    public string PackageManager { get; set; } = "auto";
    public string UpdateFeedUrl { get; set; } = AppUpdateClient.DefaultFeedUrl;
}

public sealed class AppSettingsStore
{
    private readonly string _path;

    public AppSettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFile;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions.Default)
                ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions.Default));
    }
}
