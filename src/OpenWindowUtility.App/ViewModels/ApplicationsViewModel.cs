using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenWindowUtility.Core.Catalog;
using OpenWindowUtility.Core.Packages;

namespace OpenWindowUtility.App.ViewModels;

public partial class AppItemViewModel : ObservableObject
{
    public AppItemViewModel(AppEntry entry)
    {
        Entry = entry;
    }

    public AppEntry Entry { get; }
    public string Id => Entry.Id;
    public string Category => Entry.Category;
    public bool Foss => Entry.Foss;

    public string? IconUrl => AppIcon.UrlFor(Entry);

    public string Name => App.Host.Loc.Catalog($"app.{Id}.name");
    public string Description => App.Host.Loc.Catalog($"app.{Id}.description");
    public string CategoryName => App.Host.Loc.Catalog($"category.{Category}");

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInstalled;

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(CategoryName));
    }
}

public partial class ApplicationsViewModel : PageViewModelBase
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategory = "all";

    [ObservableProperty]
    private string _selectedManager = "auto";

    public ObservableCollection<AppItemViewModel> VisibleItems { get; } = [];
    public List<AppItemViewModel> AllItems { get; }

    public IReadOnlyList<string> Categories { get; }

    public int SelectedCount => AllItems.Count(x => x.IsSelected);
    public string FooterText =>
        $"{App.Host.Loc["footer.selected"]}: {SelectedCount}    {App.Host.Loc["footer.manager"]}: WinGet, Chocolatey";

    public bool HasDetail => DetailItem is not null;

    [ObservableProperty]
    private AppItemViewModel? _detailItem;

    [ObservableProperty]
    private string _detailPublisher = "—";

    [ObservableProperty]
    private string _detailVersion = "—";

    [ObservableProperty]
    private string _detailUpdated = "—";

    private CancellationTokenSource? _detailCts;

    public ApplicationsViewModel()
    {
        AllItems = App.Host.Catalog.Apps.Items.Select(x =>
        {
            var vm = new AppItemViewModel(x);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(FooterText));
                    InstallCommand.NotifyCanExecuteChanged();
                    UninstallCommand.NotifyCanExecuteChanged();
                }
            };
            return vm;
        }).ToList();
        Categories = new[] { "all" }.Concat(AllItems.Select(x => x.Category).Distinct()).ToList();
        RebuildVisible();
    }

    partial void OnSearchTextChanged(string value) => RebuildVisible();
    partial void OnSelectedCategoryChanged(string value) => RebuildVisible();

    protected override void RefreshLanguage()
    {
        foreach (var item in AllItems)
        {
            item.RefreshText();
        }

        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(HasDetail));
        base.RefreshLanguage();
    }

    partial void OnDetailItemChanged(AppItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasDetail));
        DownloadDetailCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ShowDetailAsync(AppItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = new CancellationTokenSource();
        var ct = _detailCts.Token;
        DetailItem = item;
        DetailPublisher = PackageShowParser.PublisherFallback(item.Entry.Winget);
        DetailVersion = "—";
        DetailUpdated = "—";
        if (string.IsNullOrWhiteSpace(item.Entry.Winget))
        {
            return;
        }

        try
        {
            var details = await App.Host.Packages.GetDetailsAsync(item.Entry.Winget, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested || !ReferenceEquals(DetailItem, item))
            {
                return;
            }

            DetailPublisher = string.IsNullOrWhiteSpace(details.Publisher) ? "—" : details.Publisher;
            DetailVersion = string.IsNullOrWhiteSpace(details.Version) ? "—" : details.Version;
            DetailUpdated = string.IsNullOrWhiteSpace(details.ReleaseDate) ? "—" : details.ReleaseDate;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadDetail))]
    private async Task DownloadDetailAsync()
    {
        if (DetailItem is null)
        {
            return;
        }

        var preference = ProfileServiceParse(SelectedManager);
        var packages = new List<ResolvedPackage> { PackageResolver.Resolve(DetailItem.Entry, preference) };
        var name = DetailItem.Name;
        await App.Host.Jobs.RunAsync($"Install {name}", async (log, ct) =>
        {
            await App.Host.Packages.InstallAsync(packages, log, ct).ConfigureAwait(true);
            await RefreshInstalledAsync().ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private void CloseDetail()
    {
        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = null;
        DetailItem = null;
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in VisibleItems)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in AllItems)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task InstallAsync()
    {
        var preference = ProfileServiceParse(SelectedManager);
        var packages = SelectedEntries().Select(x => PackageResolver.Resolve(x, preference)).ToList();
        await App.Host.Jobs.RunAsync("Install apps", async (log, ct) =>
        {
            await App.Host.Packages.InstallAsync(packages, log, ct).ConfigureAwait(true);
            await RefreshInstalledAsync().ConfigureAwait(true);
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task UninstallAsync()
    {
        var preference = ProfileServiceParse(SelectedManager);
        var packages = SelectedEntries().Select(x => PackageResolver.Resolve(x, preference)).ToList();
        await App.Host.Jobs.RunAsync("Uninstall apps", async (log, ct) =>
        {
            await App.Host.Packages.UninstallAsync(packages, log, ct).ConfigureAwait(true);
            await RefreshInstalledAsync().ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task UpgradeAllAsync()
    {
        var preference = ProfileServiceParse(SelectedManager);
        await App.Host.Jobs.RunAsync("Upgrade all", async (log, ct) =>
        {
            await App.Host.Packages.UpgradeAllAsync(preference, log, ct).ConfigureAwait(true);
            await RefreshInstalledAsync().ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task RefreshInstalledAsync()
    {
        try
        {
            var installed = await App.Host.Packages.GetInstalledAppIdsAsync(
                AllItems.Select(x => x.Entry).ToList(),
                CancellationToken.None).ConfigureAwait(true);
            foreach (var item in AllItems)
            {
                item.IsInstalled = installed.Contains(item.Id);
            }
        }
        catch (Exception)
        {
            // WinGet may be missing on a fresh ISO.
        }
    }

    private bool HasSelection() => SelectedCount > 0;

    private bool CanDownloadDetail() => DetailItem is not null;

    private IEnumerable<AppEntry> SelectedEntries() =>
        AllItems.Where(x => x.IsSelected).Select(x => x.Entry);

    private void RebuildVisible()
    {
        VisibleItems.Clear();
        foreach (var item in AllItems)
        {
            if (SelectedCategory != "all" && item.Category != SelectedCategory)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(SearchText) &&
                item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) is false &&
                item.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) is false)
            {
                continue;
            }

            VisibleItems.Add(item);
        }
    }

    private static PackageManagerPreference ProfileServiceParse(string value) =>
        Enum.TryParse<PackageManagerPreference>(value, true, out var preference)
            ? preference
            : PackageManagerPreference.Auto;
}
