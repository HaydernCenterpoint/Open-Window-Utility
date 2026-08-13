using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenWindowUtility.Core.Catalog;

namespace OpenWindowUtility.App.ViewModels;

public partial class TweakItemViewModel : ObservableObject
{
    public TweakItemViewModel(TweakEntry entry)
    {
        Entry = entry;
    }

    public TweakEntry Entry { get; }
    public string Id => Entry.Id;
    public RiskLevel Risk => Entry.Risk;
    public string Name => App.Host.Loc.Catalog($"tweak.{Id}.name");
    public string Description => App.Host.Loc.Catalog($"tweak.{Id}.description");

    [ObservableProperty]
    private bool _isSelected;

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}

public partial class TweaksViewModel : PageViewModelBase
{
    public ObservableCollection<TweakItemViewModel> Essential { get; } = [];
    public ObservableCollection<TweakItemViewModel> Advanced { get; } = [];
    public ObservableCollection<TweakItemViewModel> Toggles { get; } = [];

    public IReadOnlyList<TweakItemViewModel> All { get; }

    public string LoadedText => string.Format(App.Host.Loc["tweaks.loaded"], All.Count);
    public int SelectedCount => All.Count(x => x.IsSelected);
    public string FooterText => $"{App.Host.Loc["footer.selected"]}: {SelectedCount}";

    public TweaksViewModel()
    {
        var items = App.Host.Catalog.Tweaks.Items.Select(x =>
        {
            var vm = new TweakItemViewModel(x);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TweakItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(FooterText));
                    ApplyCommand.NotifyCanExecuteChanged();
                    UndoCommand.NotifyCanExecuteChanged();
                }
            };
            return vm;
        }).ToList();
        All = items;
        foreach (var item in items)
        {
            switch (item.Risk)
            {
                case RiskLevel.Essential:
                    Essential.Add(item);
                    break;
                case RiskLevel.Advanced:
                    Advanced.Add(item);
                    break;
                case RiskLevel.Preference:
                    Toggles.Add(item);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown risk '{item.Risk}'.");
            }
        }
    }

    protected override void RefreshLanguage()
    {
        foreach (var item in All)
        {
            item.RefreshText();
        }

        OnPropertyChanged(nameof(LoadedText));
        OnPropertyChanged(nameof(FooterText));
        base.RefreshLanguage();
    }

    [RelayCommand]
    private void SelectEssential()
    {
        var preset = App.Host.Catalog.Presets.Presets.GetValueOrDefault("essential") ?? [];
        var set = preset.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in All)
        {
            item.IsSelected = set.Contains(item.Id);
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in All)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ApplyAsync()
    {
        var selected = All.Where(x => x.IsSelected).ToList();
        if (selected.Any(x => x.Risk == RiskLevel.Advanced || x.Entry.RequiresConfirm))
        {
            var result = MessageBox.Show(
                App.Host.Loc["tweaks.confirmAdvanced"],
                App.Host.Loc["app.name"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        foreach (var named in selected.Where(x => x.Entry.RequiresConfirm))
        {
            var result = MessageBox.Show(
                string.Format(App.Host.Loc["tweaks.confirmNamed"], named.Name),
                App.Host.Loc["app.name"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await App.Host.Jobs.RunAsync("Apply tweaks", async (log, ct) =>
        {
            await App.Host.Tweaks.ApplyAsync(
                selected.Select(x => x.Entry).ToList(),
                App.Host.Settings.CreateRestorePoint,
                log,
                ct).ConfigureAwait(true);
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task UndoAsync()
    {
        var ids = All.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        await App.Host.Jobs.RunAsync("Undo tweaks", async (log, ct) =>
        {
            await App.Host.Tweaks.UndoAsync(ids, log, ct).ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task TogglePreferenceAsync(TweakItemViewModel item)
    {
        if (item.Risk != RiskLevel.Preference)
        {
            return;
        }

        await App.Host.Jobs.RunAsync($"Toggle {item.Id}", async (log, ct) =>
        {
            if (item.IsSelected)
            {
                await App.Host.Tweaks.ApplyAsync(
                    [item.Entry],
                    App.Host.Settings.CreateRestorePoint,
                    log,
                    ct).ConfigureAwait(true);
            }
            else
            {
                await App.Host.Tweaks.UndoAsync([item.Id], log, ct).ConfigureAwait(true);
            }
        });
    }

    private bool HasSelection() => SelectedCount > 0;
}
