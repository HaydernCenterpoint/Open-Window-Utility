using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenWindowUtility.App.Services;
using OpenWindowUtility.Core;
using OpenWindowUtility.Core.AppUpdate;
using OpenWindowUtility.Core.Profiles;

namespace OpenWindowUtility.App.ViewModels;

public enum AppSection
{
    Applications,
    Tweaks,
    Config,
    Cleanup,
    Updates,
    Settings
}

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private AppSection _section = AppSection.Applications;

    [ObservableProperty]
    private object? _currentPage;

    public ApplicationsViewModel Applications { get; } = new();
    public TweaksViewModel Tweaks { get; } = new();
    public ConfigViewModel Config { get; } = new();
    public CleanupViewModel Cleanup { get; } = new();
    public UpdatesViewModel Updates { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    public ObservableCollection<string> LogLines { get; } = [];
    public LocalizationService Loc => App.Host.Loc;
    public bool IsBusy => App.Host.Jobs.IsBusy;

    [ObservableProperty]
    private bool _isLogExpanded;

    private UpdateUi _updateUi = UpdateUi.Idle;
    private AppRelease? _pending;
    private string _updateError = "";

    public bool HasUpdate => _updateUi == UpdateUi.Available;

    public string UpdateLabel => _updateUi switch
    {
        UpdateUi.Idle => "v" + AppPaths.Version,
        UpdateUi.Checking => Loc["appupdate.checking"],
        UpdateUi.Current => "v" + AppPaths.Version,
        UpdateUi.Available => string.Format(CultureInfo.CurrentCulture, Loc["appupdate.available"], _pending?.Version),
        UpdateUi.Downloading => Loc["appupdate.downloading"],
        UpdateUi.Failed => Loc["appupdate.failed"],
        _ => UnreachableUpdate(_updateUi)
    };

    public string UpdateTooltip => _updateUi switch
    {
        UpdateUi.Idle => string.Format(CultureInfo.CurrentCulture, Loc["appupdate.tooltip.idle"], AppPaths.Version),
        UpdateUi.Checking => Loc["appupdate.checking"],
        UpdateUi.Current => string.Format(CultureInfo.CurrentCulture, Loc["appupdate.tooltip.current"], AppPaths.Version),
        UpdateUi.Available => string.Format(CultureInfo.CurrentCulture, Loc["appupdate.tooltip.available"], _pending?.Version),
        UpdateUi.Downloading => Loc["appupdate.downloading"],
        UpdateUi.Failed => string.IsNullOrWhiteSpace(_updateError) ? Loc["appupdate.failed"] : _updateError,
        _ => UnreachableUpdate(_updateUi)
    };

    public ShellViewModel()
    {
        CurrentPage = Applications;
        App.Host.Jobs.Logged += (_, evt) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogLines.Add(evt.ToString());
                IsLogExpanded = true;
                while (LogLines.Count > 500)
                {
                    LogLines.RemoveAt(0);
                }
            });
        };
        App.Host.Loc.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(UpdateLabel));
            OnPropertyChanged(nameof(UpdateTooltip));
        };
    }

    partial void OnSectionChanged(AppSection value)
    {
        CurrentPage = value switch
        {
            AppSection.Applications => Applications,
            AppSection.Tweaks => Tweaks,
            AppSection.Config => Config,
            AppSection.Cleanup => Cleanup,
            AppSection.Updates => Updates,
            AppSection.Settings => Settings,
            _ => Unreachable(value)
        };
    }

    private static object Unreachable(AppSection value)
    {
        throw new InvalidOperationException($"Unknown section '{value}'.");
    }

    [RelayCommand]
    private void GoTo(string section)
    {
        if (Enum.TryParse<AppSection>(section, out var parsed))
        {
            Section = parsed;
        }
    }

    [RelayCommand]
    private void CancelJob() => App.Host.Jobs.Cancel();

    [RelayCommand(CanExecute = nameof(CanRunAppUpdate))]
    private async Task AppUpdateAsync()
    {
        if (_updateUi == UpdateUi.Available && _pending is not null)
        {
            await InstallAppUpdateAsync().ConfigureAwait(true);
            return;
        }

        await CheckAppUpdateAsync().ConfigureAwait(true);
    }

    private bool CanRunAppUpdate() =>
        _updateUi is not UpdateUi.Checking and not UpdateUi.Downloading;

    private async Task CheckAppUpdateAsync()
    {
        SetUpdateUi(UpdateUi.Checking);
        try
        {
            var feed = App.Host.Settings.UpdateFeedUrl;
            if (AppUpdateClient.ParseHttpsUri(feed) is null)
            {
                _updateError = Loc["appupdate.none"];
                SetUpdateUi(UpdateUi.Failed);
                return;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var found = await App.Host.AppUpdate.CheckAsync(feed, timeout.Token).ConfigureAwait(true);
            if (found is null)
            {
                _pending = null;
                SetUpdateUi(UpdateUi.Current);
                return;
            }

            _pending = found;
            SetUpdateUi(UpdateUi.Available);
        }
        catch (Exception ex)
        {
            _pending = null;
            _updateError = ex.Message;
            SetUpdateUi(UpdateUi.Failed);
        }
    }

    private async Task InstallAppUpdateAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var prompt = string.Format(
            CultureInfo.CurrentCulture,
            Loc["appupdate.confirm"],
            _pending.Version);
        if (MessageBox.Show(prompt, Loc["app.name"], MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            return;
        }

        var release = _pending;
        var feed = AppUpdateClient.ParseHttpsUri(App.Host.Settings.UpdateFeedUrl);
        if (feed is null)
        {
            _updateError = Loc["appupdate.none"];
            SetUpdateUi(UpdateUi.Failed);
            return;
        }

        if (App.Host.Jobs.IsBusy)
        {
            MessageBox.Show(Loc["busy"], Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetUpdateUi(UpdateUi.Downloading);
        string? path = null;
        var result = await App.Host.Jobs.RunAsync("Update app", async (log, ct) =>
        {
            log.Info($"Downloading {release.Version}");
            path = await App.Host.AppUpdate.DownloadAsync(release, feed, ct).ConfigureAwait(false);
            log.Info("Restarting into the new version.");
        }).ConfigureAwait(true);

        if (path is null || !result.Success)
        {
            _updateError = result.Error ?? Loc["appupdate.failed"];
            SetUpdateUi(UpdateUi.Failed);
            return;
        }

        AppUpdateApplier.ApplyAndRestart(path);
    }

    private void SetUpdateUi(UpdateUi value)
    {
        _updateUi = value;
        OnPropertyChanged(nameof(UpdateLabel));
        OnPropertyChanged(nameof(UpdateTooltip));
        OnPropertyChanged(nameof(HasUpdate));
        AppUpdateCommand.NotifyCanExecuteChanged();
    }

    private static string UnreachableUpdate(UpdateUi value) =>
        throw new InvalidOperationException($"Unknown update UI '{value}'.");

    private enum UpdateUi
    {
        Idle,
        Checking,
        Current,
        Available,
        Downloading,
        Failed
    }

    public SetupProfile BuildProfile()
    {
        return new SetupProfile
        {
            SelectedAppIds = Applications.AllItems.Where(x => x.IsSelected).Select(x => x.Id).ToList(),
            SelectedTweakIds = Tweaks.All.Where(x => x.IsSelected).Select(x => x.Id).ToList(),
            SelectedFeatureIds = Config.Selectable.Where(x => x.IsSelected).Select(x => x.Id).ToList(),
            PackageManager = Applications.SelectedManager
        };
    }

    public void ApplyProfile(SetupProfile profile)
    {
        var apps = profile.SelectedAppIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Applications.AllItems)
        {
            item.IsSelected = apps.Contains(item.Id);
        }

        var tweaks = profile.SelectedTweakIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Tweaks.All)
        {
            item.IsSelected = tweaks.Contains(item.Id);
        }

        var features = profile.SelectedFeatureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Config.Selectable)
        {
            item.IsSelected = features.Contains(item.Id);
        }

        Applications.SelectedManager = profile.PackageManager;
    }
}
