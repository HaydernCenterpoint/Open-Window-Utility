using OpenWindowUtility.Core.Catalog;

namespace OpenWindowUtility.Core.Packages;

public sealed class ResolvedPackage
{
    public required string AppId { get; init; }
    public required string Manager { get; init; }
    public required string PackageId { get; init; }
}

public static class PackageResolver
{
    public static ResolvedPackage Resolve(AppEntry app, PackageManagerPreference preference)
    {
        var hasWinget = !string.IsNullOrWhiteSpace(app.Winget);
        var hasChoco = !string.IsNullOrWhiteSpace(app.Chocolatey);

        switch (preference)
        {
            case PackageManagerPreference.Winget:
                if (hasWinget)
                {
                    return Make(app, "winget", app.Winget!);
                }

                if (hasChoco)
                {
                    return Make(app, "chocolatey", app.Chocolatey!);
                }

                break;
            case PackageManagerPreference.Chocolatey:
                if (hasChoco)
                {
                    return Make(app, "chocolatey", app.Chocolatey!);
                }

                if (hasWinget)
                {
                    return Make(app, "winget", app.Winget!);
                }

                break;
            case PackageManagerPreference.Auto:
                if (hasWinget)
                {
                    return Make(app, "winget", app.Winget!);
                }

                if (hasChoco)
                {
                    return Make(app, "chocolatey", app.Chocolatey!);
                }

                break;
            default:
                throw new InvalidOperationException($"Unknown package manager preference '{preference}'.");
        }

        throw new InvalidOperationException($"App '{app.Id}' has no install source.");
    }

    private static ResolvedPackage Make(AppEntry app, string manager, string packageId) =>
        new() { AppId = app.Id, Manager = manager, PackageId = packageId };
}
