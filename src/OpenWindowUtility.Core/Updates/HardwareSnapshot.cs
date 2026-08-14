namespace OpenWindowUtility.Core.Updates;

public sealed class DiskVolume
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => HardwareSnapshot.UsedShare(TotalBytes, FreeBytes);
}

public sealed class HardwareSnapshot
{
    public string ComputerName { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string Processor { get; init; } = "";
    public string Clock { get; init; } = "";
    public int Cores { get; init; }
    public int Threads { get; init; }
    public string Architecture { get; init; } = "";
    public long MemoryTotalBytes { get; init; }
    public long MemoryAvailableBytes { get; init; }
    public IReadOnlyList<string> Graphics { get; init; } = [];
    public IReadOnlyList<DiskVolume> Disks { get; init; } = [];

    public long MemoryUsedBytes => Math.Max(0, MemoryTotalBytes - MemoryAvailableBytes);

    public double MemoryUsedPercent => UsedShare(MemoryTotalBytes, MemoryAvailableBytes);

    public static double UsedShare(long totalBytes, long freeBytes)
    {
        if (totalBytes <= 0)
        {
            return 0;
        }

        var used = Math.Clamp(totalBytes - Math.Max(0, freeBytes), 0, totalBytes);
        return 100.0 * used / totalBytes;
    }
}
