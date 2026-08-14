using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
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

    public HardwareSnapshot GetHardware()
    {
        var memory = ReadMemory();
        return new HardwareSnapshot
        {
            ComputerName = Environment.MachineName,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Manufacturer = ReadWmiFirst("Win32_ComputerSystem", "Manufacturer"),
            Model = ReadWmiFirst("Win32_ComputerSystem", "Model"),
            Processor = ReadProcessor(out var cores, out var threads, out var clock),
            Cores = cores,
            Threads = threads,
            Clock = clock,
            MemoryTotalBytes = memory.total,
            MemoryAvailableBytes = memory.available,
            Graphics = ReadGraphics(),
            Disks = ReadDisks()
        };
    }

    private static (long total, long available) ReadMemory()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var item in searcher.Get())
            {
                var totalKb = ToInt64(item["TotalVisibleMemorySize"]);
                var freeKb = ToInt64(item["FreePhysicalMemory"]);
                return (totalKb * 1024, freeKb * 1024);
            }
        }
        catch (Exception)
        {
            // WMI can be unavailable on locked-down images.
        }

        return (0, 0);
    }

    private static string ReadProcessor(out int cores, out int threads, out string clock)
    {
        cores = 0;
        threads = 0;
        clock = "";
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                var name = item["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(name);
                }

                cores += (int)Math.Clamp(ToInt64(item["NumberOfCores"]), 0, 512);
                threads += (int)Math.Clamp(ToInt64(item["NumberOfLogicalProcessors"]), 0, 1024);
                var mhz = ToInt64(item["MaxClockSpeed"]);
                if (mhz > 0 && clock.Length == 0)
                {
                    clock = mhz >= 1000
                        ? (mhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " GHz"
                        : mhz.ToString(CultureInfo.InvariantCulture) + " MHz";
                }
            }
        }
        catch (Exception)
        {
            // WMI can be unavailable on locked-down images.
        }

        return names.Count == 0 ? "Unknown" : string.Join(" + ", names);
    }

    private static IReadOnlyList<string> ReadGraphics()
    {
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var item in searcher.Get())
            {
                var name = item["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)
                    || name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
                    || names.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                names.Add(name);
            }
        }
        catch (Exception)
        {
            // WMI can be unavailable on locked-down images.
        }

        return names;
    }

    private static IReadOnlyList<DiskVolume> ReadDisks()
    {
        var disks = new List<DiskVolume>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, VolumeName, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (var item in searcher.Get())
            {
                var id = item["DeviceID"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                disks.Add(new DiskVolume
                {
                    Id = id,
                    Label = item["VolumeName"]?.ToString()?.Trim() ?? "",
                    TotalBytes = ToInt64(item["Size"]),
                    FreeBytes = ToInt64(item["FreeSpace"])
                });
            }
        }
        catch (Exception)
        {
            // WMI can be unavailable on locked-down images.
        }

        return disks;
    }

    private static string ReadWmiFirst(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var item in searcher.Get())
            {
                var value = item[property]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch (Exception)
        {
            // WMI can be unavailable on locked-down images.
        }

        return "Unknown";
    }

    private static long ToInt64(object? value)
    {
        return value switch
        {
            null => 0,
            ulong number => number > long.MaxValue ? long.MaxValue : (long)number,
            long number => number,
            uint number => number,
            int number => number,
            _ => long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }
}
