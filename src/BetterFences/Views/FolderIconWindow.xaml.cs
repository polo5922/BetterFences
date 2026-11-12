using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        double l = Left, t = Top;
        if (!App.Config.Locked)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        if (Math.Abs(Left - l) < 3 && Math.Abs(Top - t) < 3)
        {
            Left = l;
            Top = t;
            App.Instance.TogglePopup(_folder, this);
            return;
        }

        Left = Math.Round(Left / Grid) * Grid;
        Top = Math.Round(Top / Grid) * Grid;
        _folder.X = Left;
        _folder.Y = Top;
        ConfigStore.RequestSave();
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
