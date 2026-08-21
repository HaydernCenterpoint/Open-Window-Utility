using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenWindowUtility.Core.Cleanup;

namespace OpenWindowUtility.App.ViewModels;

public partial class JunkCategoryViewModel : ObservableObject
{
    public JunkCategoryViewModel(JunkCategoryInfo info)
    {
        Info = info;
        IsSelected = !info.Deep;
    }

    public JunkCategoryInfo Info { get; }
    public JunkKind Kind => Info.Kind;
    public string Id => Info.Id;
    public bool Deep => Info.Deep;

    public string Name => App.Host.Loc[$"cleanup.cat.{Id}.name"];
    public string Description => App.Host.Loc[$"cleanup.cat.{Id}.description"];
    public string Badge => Deep ? App.Host.Loc["cleanup.deep"] : App.Host.Loc["cleanup.safe"];
    public string SizeText => CleanupFormatter.FormatBytes(Bytes);
    public string CountText => FileCount.ToString("N0", CultureInfo.CurrentCulture);
    public string CountLabel => CountText + " " + App.Host.Loc["cleanup.files"];
    public string SamplesText => Samples.Count == 0 ? "" : string.Join(Environment.NewLine, Samples);
    public bool HasSamples => Samples.Count > 0;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText))]
    [NotifyPropertyChangedFor(nameof(CountText))]
    [NotifyPropertyChangedFor(nameof(CountLabel))]
    [NotifyPropertyChangedFor(nameof(SamplesText))]
    [NotifyPropertyChangedFor(nameof(HasSamples))]
    private int _fileCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText))]
    private long _bytes;

    public IReadOnlyList<string> Samples { get; private set; } = [];

    public void ApplyHit(JunkScanHit hit)
    {
        FileCount = hit.FileCount;
        Bytes = hit.Bytes;
        Samples = hit.Samples;
        OnPropertyChanged(nameof(SamplesText));
        OnPropertyChanged(nameof(HasSamples));
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(CountLabel));
    }
}

public sealed class JunkGroupViewModel : ObservableObject
{
    public required string Id { get; init; }
    public required ObservableCollection<JunkCategoryViewModel> Items { get; init; }
    public string Name => App.Host.Loc[$"cleanup.group.{Id}"];

    public void RefreshText() => OnPropertyChanged(nameof(Name));
}

public partial class CleanupViewModel : PageViewModelBase
{
    public ObservableCollection<JunkCategoryViewModel> Categories { get; } = [];
    public ObservableCollection<JunkGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _hasScan;

    public string FooterText
    {
        get
        {
            var selected = Categories.Where(x => x.IsSelected).ToList();
            var bytes = selected.Sum(x => x.Bytes);
            var files = selected.Sum(x => x.FileCount);
            return string.Format(
                CultureInfo.CurrentCulture,
                App.Host.Loc["cleanup.footer"],
                files,
                CleanupFormatter.FormatBytes(bytes));
        }
    }

    public CleanupViewModel()
    {
        foreach (var info in App.Host.Cleanup.Categories)
        {
            var vm = new JunkCategoryViewModel(info);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(JunkCategoryViewModel.IsSelected)
                    || e.PropertyName == nameof(JunkCategoryViewModel.Bytes))
                {
                    OnPropertyChanged(nameof(FooterText));
                    CleanCommand.NotifyCanExecuteChanged();
                }
            };
            Categories.Add(vm);
        }

        foreach (var groupId in JunkCatalog.GroupOrder)
        {
            var items = new ObservableCollection<JunkCategoryViewModel>(
                Categories.Where(x => x.Info.Group == groupId));
            if (items.Count > 0)
            {
                Groups.Add(new JunkGroupViewModel { Id = groupId, Items = items });
            }
        }

        StatusText = App.Host.Loc["cleanup.status.idle"];
    }

    partial void OnHasScanChanged(bool value) => CleanCommand.NotifyCanExecuteChanged();

    protected override void RefreshLanguage()
    {
        foreach (var item in Categories)
        {
            item.RefreshText();
        }

        foreach (var group in Groups)
        {
            group.RefreshText();
        }

        if (!HasScan)
        {
            StatusText = App.Host.Loc["cleanup.status.idle"];
        }

        OnPropertyChanged(nameof(FooterText));
        base.RefreshLanguage();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IReadOnlyList<JunkScanHit>? hits = null;
        await App.Host.Jobs.RunAsync("Scan junk", async (log, ct) =>
        {
            hits = await App.Host.Cleanup.ScanAsync(log, ct).ConfigureAwait(false);
        }).ConfigureAwait(true);

        if (hits is null)
        {
            return;
        }

        var byKind = hits.ToDictionary(x => x.Kind);
        foreach (var item in Categories)
        {
            if (byKind.TryGetValue(item.Kind, out var hit))
            {
                item.ApplyHit(hit);
            }
        }

        HasScan = true;
        var totalBytes = Categories.Sum(x => x.Bytes);
        var totalFiles = Categories.Sum(x => x.FileCount);
        StatusText = string.Format(
            CultureInfo.CurrentCulture,
            App.Host.Loc["cleanup.status.done"],
            totalFiles,
            CleanupFormatter.FormatBytes(totalBytes));
        OnPropertyChanged(nameof(FooterText));
        CleanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectSafe()
    {
        foreach (var item in Categories)
        {
            item.IsSelected = !item.Deep;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var item in Categories)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var selected = Categories.Where(x => x.IsSelected && (x.Bytes > 0 || x.FileCount > 0)).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var files = selected.Sum(x => x.FileCount);
        var bytes = selected.Sum(x => x.Bytes);
        var prompt = new StringBuilder()
            .AppendFormat(
                CultureInfo.CurrentCulture,
                App.Host.Loc["cleanup.confirm"],
                files,
                CleanupFormatter.FormatBytes(bytes))
            .ToString();
        if (MessageBox.Show(prompt, App.Host.Loc["nav.cleanup"], MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes)
        {
            return;
        }

        if (selected.Any(x => x.Kind == JunkKind.WindowsOld)
            && MessageBox.Show(
                App.Host.Loc["cleanup.confirmOld"],
                App.Host.Loc["nav.cleanup"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (selected.Any(x => x.Kind == JunkKind.AiModelCache)
            && MessageBox.Show(
                App.Host.Loc["cleanup.confirmAiModels"],
                App.Host.Loc["nav.cleanup"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var kinds = selected.Select(x => x.Kind).ToList();
        await App.Host.Jobs.RunAsync("Clean junk", async (log, ct) =>
        {
            await App.Host.Cleanup.CleanAsync(kinds, log, ct).ConfigureAwait(false);
        }).ConfigureAwait(true);

        await ScanAsync().ConfigureAwait(true);
    }

    private bool CanClean() =>
        HasScan && Categories.Any(x => x.IsSelected && (x.Bytes > 0 || x.FileCount > 0));
}
