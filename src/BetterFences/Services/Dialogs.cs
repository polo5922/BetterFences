using System;
using System.Windows;
using System.Windows.Media;
using BetterFences.Views;

namespace BetterFences.Services;

/// <summary>Boîtes de dialogue ; tant qu'une est ouverte, la popup de dossier ne se ferme pas.</summary>
public static class Dialogs
{
    public static int OpenCount { get; private set; }
    public static bool IsOpen => OpenCount > 0;

    private static T Run<T>(Func<T> show)
    {
        OpenCount++;
        try { return show(); }
        finally
        {
            OpenCount--;
            App.Instance.ReactivatePopup();
        }
    }

    /// <summary>
    /// Les fenêtres de dossiers sont collées au bureau : on donne aux messages un propriétaire invisible
    /// au premier plan, pour qu'ils apparaissent devant les autres fenêtres et reçoivent le focus.
    /// </summary>
    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult def)
        => Run(() =>
        {
            var wa = SystemParameters.WorkArea;
            var owner = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
                ShowInTaskbar = false, Topmost = true, ResizeMode = ResizeMode.NoResize,
                Width = 1, Height = 1, Left = wa.Left + wa.Width / 2, Top = wa.Top + wa.Height / 2,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            owner.Show();
            owner.Activate();
            try { return MessageBox.Show(owner, message, "BetterFences", buttons, image, def); }
            finally { owner.Close(); }
        });

    public static string? Ask(string title, string initial = "")
        => Run(() => InputDialog.Show(title, initial));

    public static bool Confirm(string message)
        => Show(message, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public static bool? YesNoCancel(string message)
        => Show(message, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null,
        };

    public static void Info(string message)
        => Show(message, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
}
