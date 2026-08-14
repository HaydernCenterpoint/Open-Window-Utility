using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenWindowUtility.Core;
using OpenWindowUtility.Core.Profiles;

namespace OpenWindowUtility.App.ViewModels;

public partial class SettingsViewModel : PageViewModelBase
{
    [ObservableProperty]
    private string _languageChoice;

    [ObservableProperty]
    private bool _createRestorePoint;

    [ObservableProperty]
    private string _updateFeedUrl = "";

    public string DataPath => AppPaths.Root;

    public SettingsViewModel()
    {
        _languageChoice = App.Host.Settings.Language;
        _createRestorePoint = App.Host.Settings.CreateRestorePoint;
        _updateFeedUrl = App.Host.Settings.UpdateFeedUrl;
    }

    partial void OnLanguageChoiceChanged(string value)
    {
        App.Host.Settings.Language = value;
        App.Host.Loc.SetLanguage(value);
        App.Host.SettingsStore.Save(App.Host.Settings);
    }

    partial void OnCreateRestorePointChanged(bool value)
    {
        App.Host.Settings.CreateRestorePoint = value;
        App.Host.SettingsStore.Save(App.Host.Settings);
    }

    partial void OnUpdateFeedUrlChanged(string value)
    {
        App.Host.Settings.UpdateFeedUrl = (value ?? "").Trim();
        App.Host.SettingsStore.Save(App.Host.Settings);
    }

    [RelayCommand]
    private void ExportProfile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "owu-profile.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var shell = (ShellViewModel)Application.Current.MainWindow!.DataContext;
        App.Host.Profiles.Export(shell.BuildProfile(), dialog.FileName);
    }

    [RelayCommand]
    private void ImportProfile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var profile = App.Host.Profiles.Import(dialog.FileName);
        var shell = (ShellViewModel)Application.Current.MainWindow!.DataContext;
        shell.ApplyProfile(profile);
    }
}
