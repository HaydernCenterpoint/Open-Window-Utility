namespace OpenWindowUtility.Core.AppUpdate;

public sealed class AppRelease
{
    public required string Version { get; init; }
    public required string Url { get; init; }
    public required string Sha256 { get; init; }
}
