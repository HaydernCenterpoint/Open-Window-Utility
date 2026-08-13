using System.Globalization;
using System.Management;
using Microsoft.Win32;

namespace OpenWindowUtility.Core.Updates;

public sealed class SystemSnapshot
{
    public required string ProductName { get; init; }
    public required string DisplayVersion { get; init; }
    public required string Build { get; init; }
    public required string EditionId { get; init; }
    public required string InstallDate { get; init; }
    public required string BootTime { get; init; }
    public required string LastUpdate { get; init; }
    public required string UpdateConfiguration { get; init; }
}

public sealed class SystemInfoService
{
    private readonly Func<string> _updateLabel;

    public SystemInfoService(Func<string>? updateLabel = null)
    {
        _updateLabel = updateLabel ?? (() => "Unknown");
    }

    public SystemSnapshot GetSnapshot()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var product = key?.GetValue("ProductName") as string ?? "Windows";
        var display = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "";
        var build = $"{key?.GetValue("CurrentBuild")}.{key?.GetValue("UBR")}";
        var edition = key?.GetValue("EditionID") as string ?? "";
        var installDate = FormatUnix(key?.GetValue("InstallDate"));

        return new SystemSnapshot
        {
            ProductName = product,
            DisplayVersion = display,
            Build = build,
            EditionId = edition,
            InstallDate = installDate,
            BootTime = ReadBootTime(),
            LastUpdate = ReadLastHotfix(),
            UpdateConfiguration = _updateLabel()
        };
    }

    private static string FormatUnix(object? value)
    {
        if (value is int seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        return "Unknown";
    }

    private static string ReadBootTime()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (var item in searcher.Get())
            {
                var raw = item["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return ManagementDateTimeConverter.ToDateTime(raw).ToString("g", CultureInfo.CurrentCulture);
                }
            }
        }
        catch (Exception)
        {
            // WMI can be unavailable on locked-down images.
        }

        return "Unknown";
    }

    private static string ReadLastHotfix()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering");
            DateTime? latest = null;
            foreach (var item in searcher.Get())
            {
                var raw = item["InstalledOn"]?.ToString();
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    if (latest is null || parsed > latest)
                    {
                        latest = parsed;
                    }
                }
            }

            return latest?.ToString("d", CultureInfo.CurrentCulture) ?? "Unknown";
        }
        catch (Exception)
        {
            return "Unknown";
        }
    }
}
