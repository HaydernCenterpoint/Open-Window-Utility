namespace OpenWindowUtility.Core.Cleanup;

public enum JunkKind
{
    UserTemp,
    WindowsTemp,
    RecycleBin,
    ThumbnailCache,
    DeliveryOptimization,
    WindowsUpdateCache,
    ErrorReports,
    CrashDumps,
    DirectXShaderCache,
    BrowserCache,
    Prefetch,
    WindowsOld
}

public sealed class JunkCategoryInfo
{
    public required JunkKind Kind { get; init; }
    public required string Id { get; init; }
    public required bool Deep { get; init; }
}

public sealed class JunkRoot
{
    public required string Path { get; init; }
    public IReadOnlyList<string> Patterns { get; init; } = ["*"];
    public bool Recurse { get; init; } = true;
}

public sealed class JunkScanHit
{
    public required JunkKind Kind { get; init; }
    public required long Bytes { get; init; }
    public required int FileCount { get; init; }
    public required IReadOnlyList<string> Samples { get; init; }
}

public sealed class JunkCleanResult
{
    public required int DeletedFiles { get; init; }
    public required int SkippedFiles { get; init; }
    public required long DeletedBytes { get; init; }
}
