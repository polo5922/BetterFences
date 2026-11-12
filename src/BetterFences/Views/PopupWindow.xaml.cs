using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BetterFences.Models;
using BetterFences.Services;

namespace BetterFences.Views;

/// <summary>Popup animée qui affiche le contenu d'un dossier en mode icône ; se ferme au clic extérieur.</summary>
public partial class PopupWindow : Window
{
    private readonly Rect _anchor;
    private bool _closing;

    public FolderModel Folder { get; }

    public PopupWindow(FolderModel folder, Rect anchor)
    {
        InitializeComponent();
        Folder = folder;
        _anchor = anchor;
        DataContext = folder;
        Width = Math.Max(300, folder.Width) + 28;

        ContentView.IsPopup = true;
        ContentView.ItemLaunched += (_, _) => SafeClose();
        Deactivated += (_, _) =>
        {
            if (DragState.Active || Dialogs.IsOpen) return;
            SafeClose();
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) SafeClose(); };
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DesktopWindow.MakeToolWindow(new WindowInteropHelper(this).Handle);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Position : centrée sous la tuile, ou au-dessus si pas la place ; jamais hors de l'écran.
        var dpi = VisualTreeHelper.GetDpi(this);
        var center = new System.Drawing.Point(
            (int)((_anchor.Left + _anchor.Width / 2) * dpi.DpiScaleX),
            (int)((_anchor.Top + _anchor.Height / 2) * dpi.DpiScaleY));
        var wa = System.Windows.Forms.Screen.FromPoint(center).WorkingArea;
        var area = new Rect(wa.Left / dpi.DpiScaleX, wa.Top / dpi.DpiScaleY, wa.Width / dpi.DpiScaleX, wa.Height / dpi.DpiScaleY);

        double w = ActualWidth, h = ActualHeight;
        double left = _anchor.Left + _anchor.Width / 2 - w / 2;
        double top = _anchor.Bottom - 6;
        bool above = top + h > area.Bottom;
        if (above) top = _anchor.Top - h + 6;

        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - w));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - h));
        Root.RenderTransformOrigin = new Point(0.5, above ? 1 : 0);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var dur = TimeSpan.FromMilliseconds(170);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
        Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, 1, dur) { EasingFunction = ease });
        Scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, 1, dur) { EasingFunction = ease });
        Activate();
    }

    public void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }
}
