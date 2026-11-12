using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BetterFences.Models;
using BetterFences.Services;

namespace BetterFences.Views;

public static class MenuBuilder
{
    private static MenuItem Item(string header, Action action, bool enabled = true, bool? check = null)
    {
        var mi = new MenuItem { Header = header, IsEnabled = enabled };
        if (check.HasValue) mi.IsChecked = check.Value;
        // Laisser le menu se fermer avant d'ouvrir une éventuelle boîte de dialogue.
        mi.Click += (_, _) => mi.Dispatcher.BeginInvoke(action);
        return mi;
    }

    private static MenuItem Sub(string header, params object[] children)
    {
        var mi = new MenuItem { Header = header };
        foreach (var c in children) mi.Items.Add(c);
        return mi;
    }

    public static ContextMenu ForFolder(FolderModel f, bool inPopup)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Nouvel onglet", () => FolderActions.AddTab(f)));
        if (f.SelectedTab is { } tab)
            menu.Items.Add(Item($"Nouveau sous-onglet dans « {tab.Name} »", () => FolderActions.AddSubTab(f, tab)));
        menu.Items.Add(Item("Renommer le dossier", () => FolderActions.RenameFolder(f)));
        menu.Items.Add(new Separator());

        menu.Items.Add(Sub("Affichage",
            Item("Dossier (icône)", () => FolderActions.SetMode(f, FolderMode.Icon), check: f.Mode == FolderMode.Icon),
            Item("Panneau ouvert", () => FolderActions.SetMode(f, FolderMode.Panel), check: f.Mode == FolderMode.Panel)));

        var colors = Sub("Couleur");
        foreach (var (name, hex) in FolderActions.Colors)
        {
            var mi = Item(name, () => FolderActions.SetColor(f, hex),
                check: string.Equals(f.AccentColor, hex, StringComparison.OrdinalIgnoreCase));
            var swatch = new StackPanel { Orientation = Orientation.Horizontal };
            swatch.Children.Add(new Ellipse
            {
                Width = 12, Height = 12, Margin = new System.Windows.Thickness(0, 0, 8, 0),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            });
            swatch.Children.Add(new TextBlock { Text = name });
            mi.Header = swatch;
            colors.Items.Add(mi);
        }
        menu.Items.Add(colors);

        var opacity = Sub("Opacité du fond");
        foreach (var o in new[] { 0.6, 0.75, 0.88, 1.0 })
            opacity.Items.Add(Item($"{o * 100:0} %", () => FolderActions.SetOpacity(f, o), check: Math.Abs(f.Opacity - o) < 0.01));
        menu.Items.Add(opacity);

        if (f.Mode == FolderMode.Panel && !inPopup)
            menu.Items.Add(Item(f.Collapsed ? "Déplier" : "Réduire", () => FolderActions.ToggleCollapsed(f)));

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Nouveau dossier", () => FolderActions.NewFolder()));
        menu.Items.Add(Item("Supprimer ce dossier…", () => FolderActions.DeleteFolder(f)));
        return menu;
    }

    public static ContextMenu ForTab(FolderModel f, TabModel t)
    {
        int i = f.Tabs.IndexOf(t);
        var menu = new ContextMenu();
        menu.Items.Add(Item("Renommer l'onglet", () => FolderActions.RenameTab(f, t)));
        menu.Items.Add(Item("Ajouter un sous-onglet", () => FolderActions.AddSubTab(f, t)));
        menu.Items.Add(Item("Déplacer à gauche", () => FolderActions.MoveTab(f, t, -1), enabled: i > 0));
        menu.Items.Add(Item("Déplacer à droite", () => FolderActions.MoveTab(f, t, +1), enabled: i < f.Tabs.Count - 1));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Nouvel onglet", () => FolderActions.AddTab(f)));
        menu.Items.Add(Item("Supprimer l'onglet…", () => FolderActions.DeleteTab(f, t), enabled: f.Tabs.Count > 1));
        return menu;
    }

    public static ContextMenu ForSubTab(FolderModel f, SubTabModel s)
    {
        var tab = f.SelectedTab!;
        var menu = new ContextMenu();
        menu.Items.Add(Item("Renommer le sous-onglet", () => FolderActions.RenameSubTab(f, s)));
        menu.Items.Add(Item("Nouveau sous-onglet", () => FolderActions.AddSubTab(f, tab)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Supprimer le sous-onglet…", () => FolderActions.DeleteSubTab(f, tab, s), enabled: tab.SubTabs.Count > 1));
        return menu;
    }

    public static ContextMenu ForItem(ShortcutItem item)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Ouvrir", () => Launcher.Launch(item)));
        menu.Items.Add(Item("Exécuter en tant qu'administrateur", () => Launcher.Launch(item, asAdmin: true)));
        menu.Items.Add(Item("Ouvrir l'emplacement du fichier", () => Launcher.OpenLocation(item)));
        menu.Items.Add(Item("Renommer", () => FolderActions.RenameItem(item)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(item.Owned ? "Remettre sur le bureau" : "Retirer du dossier", () => FolderActions.RemoveItem(item)));
        return menu;
    }
}
