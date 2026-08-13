using System.Windows;

namespace OpenWindowUtility.App.Views;

public partial class ConfirmTextDialog : Window
{
    private readonly string _expected;

    public ConfirmTextDialog(string prompt, string expected)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        _expected = expected;
        Title = App.Host.Loc["app.name"];
        OkButton.Content = App.Host.Loc["updates.disable"];
        CancelButton.Content = App.Host.Loc["cancel"];
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(InputBox.Text.Trim(), _expected, StringComparison.Ordinal))
        {
            DialogResult = true;
            return;
        }

        InputBox.Focus();
    }
}
