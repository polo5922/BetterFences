using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BetterFences.Models;
using BetterFences.Services;

namespace BetterFences.Views;

/// <summary>Mode panneau : le dossier reste ouvert sur le bureau, déplaçable, redimensionnable, réductible.</summary>
public partial class FolderPanelWindow : DesktopWindow
{
    private const double Grid = 8;
    private readonly FolderModel _folder;

    public FolderPanelWindow(FolderModel folder)
    {
        InitializeComponent();
        _folder = folder;
        DataContext = folder;
        Left = folder.X;
        Top = folder.Y;
        Width = folder.Width;
        Height = folder.Height;

        ContentView.HeaderMouseDown += OnHeaderMouseDown;
        folder.PropertyChanged += OnFolderChanged;
        Closed += (_, _) => folder.PropertyChanged -= OnFolderChanged;
        ApplyState();
    }

    private void OnFolderChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolderModel.Collapsed)) ApplyState();
    }

    public void ApplyState()
    {
        ContentView.SetBodyVisible(!_folder.Collapsed);
        if (_folder.Collapsed)
        {
            SizeToContent = SizeToContent.Height;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Height = _folder.Height;
        }
        ResizeGrip.Visibility = _folder.Collapsed || App.Config.Locked ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnHeaderMouseDown(object? sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            FolderActions.ToggleCollapsed(_folder);
            return;
        }
        if (App.Config.Locked) return;
        try { DragMove(); } catch (InvalidOperationException) { return; }
        Left = Math.Round(Left / Grid) * Grid;
        Top = Math.Round(Top / Grid) * Grid;
        _folder.X = Left;
        _folder.Y = Top;
        ConfigStore.RequestSave();
    }

    private void OnResize(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(240, Width + e.HorizontalChange);
        Height = Math.Max(140, Height + e.VerticalChange);
    }

    private void OnResizeDone(object sender, DragCompletedEventArgs e)
    {
        Width = Math.Round(Width / Grid) * Grid;
        Height = Math.Round(Height / Grid) * Grid;
        _folder.Width = Width;
        _folder.Height = Height;
        ConfigStore.RequestSave();
    }
}
