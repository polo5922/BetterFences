using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BetterFences.Models;
using BetterFences.Services;

namespace BetterFences.Views;

/// <summary>Contenu d'un dossier (en-tête, onglets, sous-onglets, grille) partagé par le panneau et la popup.</summary>
public partial class FolderContentView : UserControl
{
    public FolderContentView()
    {
        InitializeComponent();
    }

    public FolderModel Folder => (FolderModel)DataContext;

    /// <summary>true dans la popup du mode icône (un clic sur une app ferme la popup).</summary>
    public bool IsPopup { get; set; }

    public event EventHandler? ItemLaunched;
    public event MouseButtonEventHandler? HeaderMouseDown;

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    // ---------- En-tête ----------

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e) => HeaderMouseDown?.Invoke(this, e);

    private void OnNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return; // un simple clic laisse l'en-tête gérer le déplacement
        e.Handled = true;
        Dispatcher.BeginInvoke(() => FolderActions.RenameFolder(Folder));
    }

    private void OnTabDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TabModel tab) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(() => FolderActions.RenameTab(Folder, tab));
    }

    private void OnSubTabDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SubTabModel sub) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(() => FolderActions.RenameSubTab(Folder, sub));
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = MenuBuilder.ForFolder(Folder, IsPopup);
        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnHorizontalWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.ScrollableWidth > 0)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }

    // ---------- Menus contextuels ----------

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        object? dc = (e.OriginalSource as FrameworkElement)?.DataContext
                     ?? (e.OriginalSource as FrameworkContentElement)?.DataContext;
        ContextMenu menu = dc switch
        {
            ShortcutItem item => MenuBuilder.ForItem(item),
            TabModel tab => MenuBuilder.ForTab(Folder, tab),
            SubTabModel sub => MenuBuilder.ForSubTab(Folder, sub),
            _ => MenuBuilder.ForFolder(Folder, IsPopup),
        };
        menu.PlacementTarget = this;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ---------- Clic / glisser des raccourcis ----------

    private ShortcutItem? _downItem;
    private Point _downPoint;
    private bool _dragged;

    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        _downItem = (sender as FrameworkElement)?.DataContext as ShortcutItem;
        _downPoint = e.GetPosition(this);
        _dragged = false;
    }

    private void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_downItem == null || _dragged || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _downPoint.X) < SystemParameters.MinimumHorizontalDragDistance * 2 &&
            Math.Abs(p.Y - _downPoint.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;

        _dragged = true;
        var item = _downItem;
        _downItem = null;

        var data = new DataObject();
        data.SetData(DragState.Format, item.Id);
        // Un raccourci déplacé dans notre stockage peut être glissé vers le bureau/l'Explorateur.
        // Les simples références (exe, dossiers…) ne sont jamais exposées pour éviter de les déplacer.
        if (item.Owned) data.SetData(DataFormats.FileDrop, new[] { item.Path });

        DragState.Item = item;
        DragState.Active = true;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data,
                DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
        }
        finally
        {
            DragState.Active = false;
            DragState.Item = null;
        }
        FolderActions.ForgetIfMovedAway(item);
    }

    private void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = (sender as FrameworkElement)?.DataContext as ShortcutItem;
        if (item != null && item == _downItem && !_dragged)
        {
            Launcher.Launch(item);
            ItemLaunched?.Invoke(this, EventArgs.Empty);
        }
        _downItem = null;
    }

    // ---------- Dépôt ----------

    private void OnDragOver(object sender, DragEventArgs e) => DropHelper.DragOver(e);

    private void OnDrop(object sender, DragEventArgs e)
    {
        var target = Folder.SelectedTab?.SelectedSubTab;
        int index = -1;

        // Dépôt sur une tuile : insertion avant/après selon le côté.
        if (FindTile(e.OriginalSource as DependencyObject) is { } tile &&
            tile.DataContext is ShortcutItem over && target != null)
        {
            index = target.Items.IndexOf(over);
            if (index >= 0 && e.GetPosition(tile).X > tile.ActualWidth / 2) index++;
        }
        DropHelper.Drop(Folder, target, index, e);
    }

    private static FrameworkElement? FindTile(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is FrameworkElement { Tag: "tile" } fe) return fe;
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    // Survoler un onglet/sous-onglet pendant un glisser l'ouvre (comme sur Android).
    private void OnTabDragEnter(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TabModel tab) Folder.SelectedTab = tab;
    }

    private void OnSubTabDragEnter(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SubTabModel sub && Folder.SelectedTab is { } tab)
            tab.SelectedSubTab = sub;
    }
}
