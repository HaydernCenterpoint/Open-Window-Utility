using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenWindowUtility.Core.Catalog;

namespace OpenWindowUtility.App.ViewModels;

public partial class FeatureItemViewModel : ObservableObject
{
    public FeatureItemViewModel(FeatureEntry entry)
    {
        Entry = entry;
        IsEditionSupported = App.Host.Features.IsEditionSupported(entry);
    }

    public FeatureEntry Entry { get; }
    public string Id => Entry.Id;
    public FeatureKind Kind => Entry.Kind;
    public bool IsEditionSupported { get; }
    public string Name => App.Host.Loc.Catalog($"feature.{Id}.name");
    public string Description => App.Host.Loc.Catalog($"feature.{Id}.description");

    [ObservableProperty]
    private bool _isSelected;

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}

public partial class ConfigViewModel : PageViewModelBase
{
    public ObservableCollection<FeatureItemViewModel> Features { get; } = [];
    public ObservableCollection<FeatureItemViewModel> Fixes { get; } = [];
    public ObservableCollection<FeatureItemViewModel> Panels { get; } = [];
    public IReadOnlyList<FeatureItemViewModel> Selectable { get; }

    public int SelectedCount => Selectable.Count(x => x.IsSelected);
    public string FooterText => $"{App.Host.Loc["footer.selected"]}: {SelectedCount}";

    public ConfigViewModel()
    {
        var all = new List<FeatureItemViewModel>();
        foreach (var entry in App.Host.Catalog.Features.Items)
        {
            var vm = new FeatureItemViewModel(entry);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FeatureItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(FooterText));
                    ApplyCommand.NotifyCanExecuteChanged();
                }
            };
            all.Add(vm);
            switch (entry.Kind)
            {
                case FeatureKind.Feature:
                    Features.Add(vm);
                    break;
                case FeatureKind.Fix:
                    Fixes.Add(vm);
                    break;
                case FeatureKind.Panel:
                    Panels.Add(vm);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown feature kind '{entry.Kind}'.");
            }
        }

        Selectable = all.Where(x => x.Kind != FeatureKind.Panel).ToList();
    }

    protected override void RefreshLanguage()
    {
        foreach (var item in Features.Concat(Fixes).Concat(Panels))
        {
            item.RefreshText();
        }

        OnPropertyChanged(nameof(FooterText));
        base.RefreshLanguage();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ApplyAsync()
    {
        var selected = Selectable.Where(x => x.IsSelected).ToList();
        var unsupported = selected.Where(x => !x.IsEditionSupported).ToList();
        if (unsupported.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, unsupported.Select(x => x.Name)),
                App.Host.Loc["app.name"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        await App.Host.Jobs.RunAsync("Apply features", async (log, ct) =>
        {
            await App.Host.Features.ApplyAsync(selected.Select(x => x.Entry).ToList(), log, ct)
                .ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task OpenPanelAsync(FeatureItemViewModel item)
    {
        await App.Host.Jobs.RunAsync(item.Name, async (log, ct) =>
        {
            await App.Host.Features.ApplyAsync([item.Entry], log, ct).ConfigureAwait(true);
        });
    }

    private bool HasSelection() => SelectedCount > 0;
}
