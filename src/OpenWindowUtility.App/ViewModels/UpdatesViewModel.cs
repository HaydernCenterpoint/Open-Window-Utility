using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenWindowUtility.Core.Updates;

namespace OpenWindowUtility.App.ViewModels;

public partial class UpdatesViewModel : PageViewModelBase
{
    [ObservableProperty]
    private string _productName = "";

    [ObservableProperty]
    private string _displayVersion = "";

    [ObservableProperty]
    private string _build = "";

    [ObservableProperty]
    private string _installDate = "";

    [ObservableProperty]
    private string _bootTime = "";

    [ObservableProperty]
    private string _lastUpdate = "";

    [ObservableProperty]
    private string _updateConfiguration = "";

    public string AppsVersion => App.Host.Catalog.Apps.Version;
    public string TweaksVersion => App.Host.Catalog.Tweaks.Version;
    public string FeaturesVersion => App.Host.Catalog.Features.Version;
    public string AppVersion => Core.AppPaths.Version;

    public UpdatesViewModel()
    {
        RefreshSnapshot();
    }

    protected override void RefreshLanguage()
    {
        RefreshSnapshot();
        base.RefreshLanguage();
    }

    [RelayCommand]
    private async Task ApplyDefaultAsync()
    {
        await ApplyAsync(UpdatePolicyKind.Default).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ApplySecurityAsync()
    {
        await ApplyAsync(UpdatePolicyKind.Security).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ApplyDisableAsync()
    {
        var dialog = new Views.ConfirmTextDialog(
            App.Host.Loc["updates.confirmDisable"],
            "DISABLE")
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ApplyAsync(UpdatePolicyKind.DisableAll).ConfigureAwait(true);
    }

    private async Task ApplyAsync(UpdatePolicyKind kind)
    {
        await App.Host.Jobs.RunAsync($"Update policy {kind}", async (log, ct) =>
        {
            await App.Host.Updates.ApplyAsync(kind, log, ct).ConfigureAwait(true);
            Application.Current.Dispatcher.Invoke(RefreshSnapshot);
        });
    }

    private void RefreshSnapshot()
    {
        var snapshot = App.Host.SystemInfo.GetSnapshot();
        ProductName = snapshot.ProductName;
        DisplayVersion = snapshot.DisplayVersion;
        Build = snapshot.Build;
        InstallDate = snapshot.InstallDate;
        BootTime = snapshot.BootTime;
        LastUpdate = snapshot.LastUpdate;
        UpdateConfiguration = snapshot.UpdateConfiguration;
    }
}
