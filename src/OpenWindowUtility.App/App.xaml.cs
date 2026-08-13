using System.Windows;
using System.Windows.Media;
using OpenWindowUtility.Core;
using Wpf.Ui.Appearance;

namespace OpenWindowUtility.App;

public partial class App : Application
{
    public static AppHost Host { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!Elevation.IsAdministrator())
        {
            MessageBox.Show(
                Services.UiCopy.English["needAdmin"] + Environment.NewLine + Services.UiCopy.Vietnamese["needAdmin"],
                "Open Window Utility",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        try
        {
            Host = AppHost.Create();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open Window Utility", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        ApplicationAccentColorManager.Apply(Color.FromRgb(0x4A, 0x7D, 0x76));
        base.OnStartup(e);
    }
}
