using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenWindowUtility.Core.Creator;

namespace OpenWindowUtility.App.ViewModels;

public partial class Win11CreatorViewModel : PageViewModelBase
{
    [ObservableProperty]
    private bool _bypassTpm = true;

    [ObservableProperty]
    private bool _bypassSecureBoot = true;

    [ObservableProperty]
    private bool _bypassRam = true;

    [ObservableProperty]
    private bool _bypassCpu = true;

    [ObservableProperty]
    private bool _bypassStorage = true;

    [ObservableProperty]
    private bool _bypassDiskCheck = true;

    [ObservableProperty]
    private bool _disableBitLocker = true;

    [ObservableProperty]
    private bool _bypassMicrosoftAccount = true;

    [ObservableProperty]
    private bool _disableOobeTelemetry = true;

    [ObservableProperty]
    private bool _disableConsumerExperience = true;

    [ObservableProperty]
    private bool _skipEula = true;

    [ObservableProperty]
    private string _localUsername = "User";

    [ObservableProperty]
    private string _localPassword = string.Empty;

    [ObservableProperty]
    private bool _autoLogon = false;

    [ObservableProperty]
    private string _computerName = string.Empty;

    [ObservableProperty]
    private bool _enableFirstLogonTweaks = true;

    [ObservableProperty]
    private bool _isIsoMode = false;

    [ObservableProperty]
    private string _sourceIsoPath = string.Empty;

    [ObservableProperty]
    private string _outputIsoPath = string.Empty;

    public bool IsXmlMode => !IsIsoMode;

    partial void OnIsIsoModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsXmlMode));
    }

    [RelayCommand]
    private void SelectAllBypasses()
    {
        BypassTpm = true;
        BypassSecureBoot = true;
        BypassRam = true;
        BypassCpu = true;
        BypassStorage = true;
        BypassDiskCheck = true;
        DisableBitLocker = true;
        BypassMicrosoftAccount = true;
        DisableOobeTelemetry = true;
        DisableConsumerExperience = true;
        SkipEula = true;
    }

    [RelayCommand]
    private void DeselectAllBypasses()
    {
        BypassTpm = false;
        BypassSecureBoot = false;
        BypassRam = false;
        BypassCpu = false;
        BypassStorage = false;
        BypassDiskCheck = false;
        DisableBitLocker = false;
        BypassMicrosoftAccount = false;
        DisableOobeTelemetry = false;
        DisableConsumerExperience = false;
        SkipEula = false;
    }

    [RelayCommand]
    private void BrowseSourceIso()
    {
        var dialog = new OpenFileDialog
        {
            Title = App.Host.Loc["win11.select.source"],
            Filter = "ISO image (*.iso)|*.iso|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            SourceIsoPath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(OutputIsoPath))
            {
                var dir = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                var name = Path.GetFileNameWithoutExtension(dialog.FileName);
                OutputIsoPath = Path.Combine(dir, $"{name}_custom.iso");
            }
        }
    }

    [RelayCommand]
    private void BrowseOutputIso()
    {
        var dialog = new SaveFileDialog
        {
            Title = App.Host.Loc["win11.select.output"],
            Filter = "ISO image (*.iso)|*.iso|All files (*.*)|*.*",
            DefaultExt = ".iso",
            FileName = string.IsNullOrWhiteSpace(OutputIsoPath) ? "Windows11_Custom.iso" : Path.GetFileName(OutputIsoPath)
        };

        if (dialog.ShowDialog() == true)
        {
            OutputIsoPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task ExportXmlAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = App.Host.Loc["win11.action.exportXml"],
            Filter = "Unattended Answer File (*.xml)|*.xml|All files (*.*)|*.*",
            FileName = "autounattend.xml",
            DefaultExt = ".xml"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var config = BuildConfig();
        try
        {
            await App.Host.IsoEngine.ExportUnattendedXmlAsync(dialog.FileName, config).ConfigureAwait(true);
            var msg = string.Format(CultureInfo.CurrentCulture, App.Host.Loc["win11.xml.success"], dialog.FileName);
            MessageBox.Show(msg, App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task BuildIsoAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceIsoPath) || !File.Exists(SourceIsoPath))
        {
            MessageBox.Show(App.Host.Loc["win11.source.missing"], App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputIsoPath))
        {
            MessageBox.Show(App.Host.Loc["win11.output.missing"], App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (App.Host.Jobs.IsBusy)
        {
            MessageBox.Show(App.Host.Loc["busy"], App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var config = BuildConfig();
        var source = SourceIsoPath;
        var output = OutputIsoPath;

        var result = await App.Host.Jobs.RunAsync(App.Host.Loc["win11.action.buildIso"], async (log, ct) =>
        {
            await App.Host.IsoEngine.BuildCustomIsoAsync(source, output, config, log, ct).ConfigureAwait(false);
        }).ConfigureAwait(true);

        if (result.Success)
        {
            var msg = string.Format(CultureInfo.CurrentCulture, App.Host.Loc["win11.iso.success"], output);
            MessageBox.Show(msg, App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (result.Error != "Cancelled")
        {
            MessageBox.Show(result.Error ?? App.Host.Loc["failed"], App.Host.Loc["app.name"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public Win11CreatorConfig BuildConfig()
    {
        return new Win11CreatorConfig
        {
            BypassTpm = BypassTpm,
            BypassSecureBoot = BypassSecureBoot,
            BypassRam = BypassRam,
            BypassCpu = BypassCpu,
            BypassStorage = BypassStorage,
            BypassDiskCheck = BypassDiskCheck,
            DisableBitLockerAutomaticDeviceEncryption = DisableBitLocker,
            BypassMicrosoftAccount = BypassMicrosoftAccount,
            DisableOobeTelemetry = DisableOobeTelemetry,
            DisableConsumerExperience = DisableConsumerExperience,
            SkipEula = SkipEula,
            LocalUsername = string.IsNullOrWhiteSpace(LocalUsername) ? "User" : LocalUsername.Trim(),
            LocalPassword = LocalPassword ?? string.Empty,
            AutoLogon = AutoLogon,
            ComputerName = string.IsNullOrWhiteSpace(ComputerName) ? null : ComputerName.Trim(),
            EnableFirstLogonTweaks = EnableFirstLogonTweaks
        };
    }
}
