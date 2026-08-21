using OpenWindowUtility.Core;
using OpenWindowUtility.Core.AppUpdate;
using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Cleanup;
using OpenWindowUtility.Core.Creator;
using OpenWindowUtility.Core.Features;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;
using OpenWindowUtility.Core.Packages;
using OpenWindowUtility.Core.Profiles;
using OpenWindowUtility.Core.Safety;
using OpenWindowUtility.Core.Settings;
using OpenWindowUtility.Core.Tweaks;
using OpenWindowUtility.Core.Updates;
using OpenWindowUtility.App.Services;

namespace OpenWindowUtility.App;

public sealed class AppHost
{
    public required LoadedCatalog Catalog { get; init; }
    public required JobRunner Jobs { get; init; }
    public required LocalizationService Loc { get; init; }
    public required PackageEngine Packages { get; init; }
    public required TweakEngine Tweaks { get; init; }
    public required FeatureEngine Features { get; init; }
    public required UpdatePolicyEngine Updates { get; init; }
    public required SystemInfoService SystemInfo { get; init; }
    public required AppSettingsStore SettingsStore { get; init; }
    public required AppSettings Settings { get; init; }
    public required ProfileService Profiles { get; init; }
    public required UndoJournal Journal { get; init; }
    public required CleanupEngine Cleanup { get; init; }
    public required AppUpdateClient AppUpdate { get; init; }
    public required IsoEngine IsoEngine { get; init; }

    public static AppHost Create()
    {
        AppPaths.EnsureCreated();
        var catalog = new CatalogLoader().Load();
        var settingsStore = new AppSettingsStore();
        var settings = settingsStore.Load();
        var loc = new LocalizationService(catalog.I18n);
        loc.ApplySettings(settings);
        var runner = new ProcessRunner();
        var restore = new RestorePointService();
        var journal = new UndoJournal();
        var executor = new WindowsOperationExecutor(runner, restore);
        var updates = new UpdatePolicyEngine(executor, journal);
        var systemInfo = new SystemInfoService(updates.ReadCurrentLabel);
        return new AppHost
        {
            Catalog = catalog,
            Jobs = new JobRunner(),
            Loc = loc,
            Packages = new PackageEngine(runner),
            Tweaks = new TweakEngine(executor, journal, restore),
            Features = new FeatureEngine(executor, systemInfo),
            Updates = updates,
            SystemInfo = systemInfo,
            SettingsStore = settingsStore,
            Settings = settings,
            Profiles = new ProfileService(),
            Journal = journal,
            Cleanup = new CleanupEngine(),
            AppUpdate = new AppUpdateClient(),
            IsoEngine = new IsoEngine(runner)
        };
    }
}
