using System.Reflection;
using System.Text.Json;
using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Catalog;

public sealed class CatalogLoader
{
    private readonly Assembly _assembly;

    public CatalogLoader(Assembly? assembly = null)
    {
        _assembly = assembly ?? typeof(CatalogLoader).Assembly;
    }

    public LoadedCatalog Load()
    {
        var apps = Read<AppCatalogFile>("catalogs.apps.json");
        var tweaks = Read<TweakCatalogFile>("catalogs.tweaks.json");
        var features = Read<FeatureCatalogFile>("catalogs.features.json");
        var presets = Read<PresetCatalogFile>("catalogs.presets.json");
        var i18n = CatalogI18n.LoadFromJson(ReadString("catalogs.i18n.en.json"), ReadString("catalogs.i18n.vi.json"));
        var loaded = new LoadedCatalog
        {
            Apps = apps,
            Tweaks = tweaks,
            Features = features,
            Presets = presets,
            I18n = i18n
        };
        Validate(loaded);
        return loaded;
    }

    public static void Validate(LoadedCatalog catalog)
    {
        var errors = new List<string>();
        ValidateApps(catalog, errors);
        ValidateTweaks(catalog, errors);
        ValidateFeatures(catalog, errors);
        ValidatePresets(catalog, errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Catalog validation failed:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateApps(LoadedCatalog catalog, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog.Apps.Items)
        {
            if (!ids.Add(app.Id))
            {
                errors.Add($"Duplicate app id '{app.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(app.Winget) && string.IsNullOrWhiteSpace(app.Chocolatey))
            {
                errors.Add($"App '{app.Id}' has no package id.");
            }

            if (app.Winget is { Length: > 0 } && !ProcessAllowlist.IsSafePackageId(app.Winget))
            {
                errors.Add($"App '{app.Id}' has an unsafe WinGet id.");
            }

            if (app.Chocolatey is { Length: > 0 } && !ProcessAllowlist.IsSafePackageId(app.Chocolatey))
            {
                errors.Add($"App '{app.Id}' has an unsafe Chocolatey id.");
            }

            if (app.Icon is { Length: > 0 } && AppIcon.TryHttps(app.Icon) is null)
            {
                errors.Add($"App '{app.Id}' icon is not an https URL.");
            }

            if (AppIcon.UrlFor(app) is not { } iconUrl || !AppIcon.IsTrusted(app, iconUrl))
            {
                errors.Add($"App '{app.Id}' has no trusted https icon or homepage.");
            }

            RequireKey(catalog.I18n, $"app.{app.Id}.name", errors);
            RequireKey(catalog.I18n, $"app.{app.Id}.description", errors);
            RequireKey(catalog.I18n, $"category.{app.Category}", errors);
        }
    }

    private static void ValidateTweaks(LoadedCatalog catalog, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tweak in catalog.Tweaks.Items)
        {
            if (!ids.Add(tweak.Id))
            {
                errors.Add($"Duplicate tweak id '{tweak.Id}'.");
            }

            if (tweak.Apply.Count == 0)
            {
                errors.Add($"Tweak '{tweak.Id}' has no apply operations.");
            }

            ValidateOperations($"tweak {tweak.Id} apply", tweak.Apply, errors);
            ValidateOperations($"tweak {tweak.Id} undo", tweak.Undo, errors);
            RequireKey(catalog.I18n, $"tweak.{tweak.Id}.name", errors);
            RequireKey(catalog.I18n, $"tweak.{tweak.Id}.description", errors);
        }
    }

    private static void ValidateFeatures(LoadedCatalog catalog, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in catalog.Features.Items)
        {
            if (!ids.Add(feature.Id))
            {
                errors.Add($"Duplicate feature id '{feature.Id}'.");
            }

            if (feature.Apply.Count == 0)
            {
                errors.Add($"Feature '{feature.Id}' has no apply operations.");
            }

            ValidateOperations($"feature {feature.Id} apply", feature.Apply, errors);
            ValidateOperations($"feature {feature.Id} undo", feature.Undo, errors);
            RequireKey(catalog.I18n, $"feature.{feature.Id}.name", errors);
            RequireKey(catalog.I18n, $"feature.{feature.Id}.description", errors);
        }
    }

    private static void ValidatePresets(LoadedCatalog catalog, List<string> errors)
    {
        var tweakIds = catalog.Tweaks.Items.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, ids) in catalog.Presets.Presets)
        {
            foreach (var id in ids)
            {
                if (!tweakIds.Contains(id))
                {
                    errors.Add($"Preset '{name}' references unknown tweak '{id}'.");
                }
            }
        }
    }

    private static void ValidateOperations(string context, IEnumerable<Operation> operations, List<string> errors)
    {
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case ProcessOperation process when !ProcessAllowlist.IsAllowed(process.FileName, process.UseShellExecute):
                    errors.Add($"{context}: process '{process.FileName}' is not allowlisted.");
                    break;
                case AppxRemoveOperation appx when !ProcessAllowlist.IsSafeAppxName(appx.PackageName):
                    errors.Add($"{context}: unsafe AppX name '{appx.PackageName}'.");
                    break;
                case RegistrySetOperation:
                case RegistryDeleteOperation:
                case ServiceStartupOperation:
                case ScheduledTaskOperation:
                case AppxRemoveOperation:
                case ProcessOperation:
                case DismFeatureOperation:
                case PowerPlanOperation:
                case CreateRestorePointOperation:
                    break;
                default:
                    errors.Add($"{context}: unsupported operation '{operation.GetType().Name}'.");
                    break;
            }
        }
    }

    private static void RequireKey(CatalogI18n i18n, string key, List<string> errors)
    {
        if (!i18n.EnglishKeys.Contains(key))
        {
            errors.Add($"Missing English i18n key '{key}'.");
        }
    }

    private T Read<T>(string resourceName)
    {
        var json = ReadString(resourceName);
        return JsonSerializer.Deserialize<T>(json, JsonOptions.Default)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' deserialized to null.");
    }

    private string ReadString(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
