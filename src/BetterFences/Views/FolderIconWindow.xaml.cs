using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BetterFences.Models;
using BetterFences.Services;

namespace BetterFences.Views;

/// <summary>Mode icône : une tuile de dossier façon Android (aperçu 2×2) qui ouvre une popup au clic.</summary>
public partial class FolderIconWindow : DesktopWindow
{
    private const double Grid = 8;
    private readonly FolderModel _folder;

    public FolderIconWindow(FolderModel folder)
    {
        InitializeComponent();
        _folder = folder;
        DataContext = folder;
        Left = folder.X;
        Top = folder.Y;
    }

    public Rect TileScreenRect
    {
        get
        {
            var topLeft = Tile.TranslatePoint(new Point(0, 0), this);
            return new Rect(Left + topLeft.X, Top + topLeft.Y, Tile.ActualWidth, Tile.ActualHeight);
        }
    }

    private DispatcherTimer? _openTimer;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        bool onLabel = IsWithin(e.OriginalSource as DependencyObject, NameLabel);

        // Double-clic sur le nom : renommer le dossier.
        if (e.ClickCount == 2 && onLabel)
        {
            _openTimer?.Stop();
            App.Instance.ClosePopup();
            e.Handled = true;
            Dispatcher.BeginInvoke(() => FolderActions.RenameFolder(_folder));
            return;
        }

        // La popup de ce dossier était ouverte : ce clic sert juste à la fermer.
        bool popupWasOpen = App.Instance.IsPopupOpenOrJustClosed(_folder);

        double l = Left, t = Top;
        if (!App.Config.Locked)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        if (Math.Abs(Left - l) < 3 && Math.Abs(Top - t) < 3)
        {
            Left = l;
            Top = t;
            if (onLabel && !popupWasOpen)
            {
                // Clic sur le nom : on attend de savoir si c'est un double-clic avant d'ouvrir.
                _openTimer ??= new DispatcherTimer(
                    TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime),
                    DispatcherPriority.Input, (_, _) => { _openTimer!.Stop(); App.Instance.TogglePopup(_folder, this); },
                    Dispatcher);
                _openTimer.Stop();
                _openTimer.Start();
            }
            else
            {
                App.Instance.TogglePopup(_folder, this);
            }
            return;
        }

        Left = Math.Round(Left / Grid) * Grid;
        Top = Math.Round(Top / Grid) * Grid;
        _folder.X = Left;
        _folder.Y = Top;
        ConfigStore.RequestSave();
    }

    private static bool IsWithin(DependencyObject? d, DependencyObject target)
    {
        for (; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d == target) return true;
        return false;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = MenuBuilder.ForFolder(_folder, inPopup: false);
        menu.PlacementTarget = this;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e) => DropHelper.DragOver(e);

    private void OnDrop(object sender, DragEventArgs e)
        => DropHelper.Drop(_folder, _folder.SelectedTab?.SelectedSubTab, -1, e);
}
