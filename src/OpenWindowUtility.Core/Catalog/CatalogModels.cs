using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Catalog;

public enum RiskLevel
{
    Essential,
    Advanced,
    Preference
}

public enum FeatureKind
{
    Feature,
    Fix,
    Panel
}

public enum PackageManagerPreference
{
    Auto,
    Winget,
    Chocolatey
}

public sealed class AppCatalogFile
{
    public string Version { get; init; } = "0";
    public List<AppEntry> Items { get; init; } = [];
}

public sealed class AppEntry
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public string? Winget { get; init; }
    public string? Chocolatey { get; init; }
    public bool Foss { get; init; }
    public string? Homepage { get; init; }
    public string? Icon { get; init; }
}

public sealed class TweakCatalogFile
{
    public string Version { get; init; } = "0";
    public List<TweakEntry> Items { get; init; } = [];
}

public sealed class TweakEntry
{
    public required string Id { get; init; }
    public required RiskLevel Risk { get; init; }
    public required string Category { get; init; }
    public bool RequiresConfirm { get; init; }
    public List<Operation> Apply { get; init; } = [];
    public List<Operation> Undo { get; init; } = [];
}

public sealed class FeatureCatalogFile
{
    public string Version { get; init; } = "0";
    public List<FeatureEntry> Items { get; init; } = [];
}

public sealed class FeatureEntry
{
    public required string Id { get; init; }
    public required FeatureKind Kind { get; init; }
    public List<string> Editions { get; init; } = [];
    public List<Operation> Apply { get; init; } = [];
    public List<Operation> Undo { get; init; } = [];
}

public sealed class PresetCatalogFile
{
    public string Version { get; init; } = "0";
    public Dictionary<string, List<string>> Presets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LoadedCatalog
{
    public required AppCatalogFile Apps { get; init; }
    public required TweakCatalogFile Tweaks { get; init; }
    public required FeatureCatalogFile Features { get; init; }
    public required PresetCatalogFile Presets { get; init; }
    public required CatalogI18n I18n { get; init; }
}
