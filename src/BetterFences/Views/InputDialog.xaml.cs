using System.Windows;
using System.Windows.Input;

namespace BetterFences.Views;

public partial class InputDialog : Window
{
    private InputDialog(string title, string initial)
    {
        InitializeComponent();
        TitleText.Text = title;
        Input.Text = initial;
        Loaded += (_, _) =>
        {
            Activate();
            Input.Focus();
            Input.SelectAll();
        };
    }

    public static string? Show(string title, string initial)
    {
        var dlg = new InputDialog(title, initial);
        return dlg.ShowDialog() == true ? dlg.Input.Text : null;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
