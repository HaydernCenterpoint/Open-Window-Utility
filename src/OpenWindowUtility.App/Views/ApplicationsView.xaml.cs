using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using OpenWindowUtility.App.ViewModels;

namespace OpenWindowUtility.App.Views;

public partial class ApplicationsView
{
    public ApplicationsView()
    {
        InitializeComponent();
    }

    private void AppCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FromCheckBox(e.OriginalSource as DependencyObject)
            || sender is not FrameworkElement { DataContext: AppItemViewModel item }
            || DataContext is not ApplicationsViewModel vm)
        {
            return;
        }

        vm.ShowDetailCommand.Execute(item);
    }

    private void AppIcon_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (sender is Image image)
        {
            image.Opacity = 1;
        }
    }

    private void AppIcon_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is Image image)
        {
            image.Opacity = 0;
        }
    }

    private static bool FromCheckBox(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is CheckBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
