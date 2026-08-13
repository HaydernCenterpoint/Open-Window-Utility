using Wpf.Ui.Controls;
using OpenWindowUtility.App.ViewModels;

namespace OpenWindowUtility.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel();
    }
}
