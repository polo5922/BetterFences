using System.Linq;
using System.Windows;
using BetterFences.Models;

namespace BetterFences.Services;

/// <summary>Toutes les opérations qui modifient les dossiers, onglets, sous-onglets et raccourcis.</summary>
public static class FolderActions
{
    public static readonly (string Name, string Hex)[] Colors =
    {
        ("Bleu", "#4F7CFF"), ("Violet", "#8B5CF6"), ("Rose", "#EC4899"), ("Rouge", "#EF4444"),
        ("Orange", "#F59E0B"), ("Vert", "#22C55E"), ("Turquoise", "#14B8A6"), ("Gris", "#9CA3AF"),
    };

    // ---------- Dossiers ----------

    public static FolderModel NewFolder()
    {
        var wa = SystemParameters.WorkArea;
        var folder = new FolderModel
        {
            Name = "Nouveau dossier",
            X = wa.Left + wa.Width / 2 - 50,
            Y = wa.Top + wa.Height / 2 - 60,
        };
        folder.Tabs.Add(new TabModel("Général"));
        folder.EnsureValid();
        App.Config.Folders.Add(folder);
        App.Instance.ShowFolder(folder);
        ConfigStore.RequestSave();
        return folder;
    }

    public static void RenameFolder(FolderModel f)
    {
        var name = Dialogs.Ask("Nom du dossier", f.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        f.Name = name.Trim();
        ConfigStore.RequestSave();
    }

    public static void SetMode(FolderModel f, FolderMode mode)
    {
        if (f.Mode == mode) return;
        f.Mode = mode;
        f.Collapsed = false;
        App.Instance.ClosePopup();
        App.Instance.ShowFolder(f);
        ConfigStore.RequestSave();
    }

    public static void ToggleCollapsed(FolderModel f)
    {
        f.Collapsed = !f.Collapsed;
        ConfigStore.RequestSave();
    }

    public static void SetColor(FolderModel f, string hex) { f.AccentColor = hex; ConfigStore.RequestSave(); }
    public static void SetOpacity(FolderModel f, double o) { f.Opacity = o; ConfigStore.RequestSave(); }

    public static void DeleteFolder(FolderModel f)
    {
        int n = f.AllItems.Count();
        string msg = n > 0
            ? $"Supprimer le dossier « {f.Name} » ?\n\nSes {n} raccourci(s) seront remis sur le bureau."
            : $"Supprimer le dossier « {f.Name} » ?";
        if (!Dialogs.Confirm(msg)) return;
        foreach (var item in f.AllItems.ToList()) ShortcutStorage.Restore(item);
        App.Instance.ClosePopup();
        App.Instance.CloseFolderWindow(f);
        App.Config.Folders.Remove(f);
        ConfigStore.RequestSave();
    }

    // ---------- Onglets ----------

    public static void AddTab(FolderModel f)
    {
        var name = Dialogs.Ask("Nom du nouvel onglet", $"Onglet {f.Tabs.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;
        var tab = new TabModel(name.Trim());
        f.Tabs.Add(tab);
        f.SelectedTab = tab;
        f.NotifyChanged();
    }

    public static void RenameTab(FolderModel f, TabModel t)
    {
        var name = Dialogs.Ask("Nom de l'onglet", t.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        t.Name = name.Trim();
        f.NotifyChanged();
    }

    public static void MoveTab(FolderModel f, TabModel t, int delta)
    {
        int i = f.Tabs.IndexOf(t), j = i + delta;
        if (i < 0 || j < 0 || j >= f.Tabs.Count) return;
        f.Tabs.Move(i, j);
        f.SelectedTab = t;
        f.NotifyChanged();
    }

    public static void DeleteTab(FolderModel f, TabModel t)
    {
        if (f.Tabs.Count <= 1) return;
        var items = t.SubTabs.SelectMany(s => s.Items).ToList();
        string msg = items.Count > 0
            ? $"Supprimer l'onglet « {t.Name} » ?\n\nSes {items.Count} raccourci(s) seront remis sur le bureau."
            : $"Supprimer l'onglet « {t.Name} » ?";
        if (!Dialogs.Confirm(msg)) return;
        foreach (var item in items) ShortcutStorage.Restore(item);
        f.Tabs.Remove(t);
        f.SelectedTabIndex = 0;
        f.NotifyChanged();
    }

    // ---------- Sous-onglets ----------

    public static void AddSubTab(FolderModel f, TabModel t)
    {
        var name = Dialogs.Ask($"Nouveau sous-onglet dans « {t.Name} »", $"Sous-onglet {t.SubTabs.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;
        var sub = new SubTabModel(name.Trim());
        t.SubTabs.Add(sub);
        t.SelectedSubTab = sub;
        f.SelectedTab = t;
        f.NotifyChanged();
    }

    public static void RenameSubTab(FolderModel f, SubTabModel s)
    {
        var name = Dialogs.Ask("Nom du sous-onglet", s.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        s.Name = name.Trim();
        f.NotifyChanged();
    }

    /// <summary>Supprime un sous-onglet ; ses raccourcis passent dans le premier sous-onglet restant.</summary>
    public static void DeleteSubTab(FolderModel f, TabModel t, SubTabModel s)
    {
        if (t.SubTabs.Count <= 1) return;
        if (!Dialogs.Confirm($"Supprimer le sous-onglet « {s.Name} » ?\n\nSes raccourcis seront déplacés dans un autre sous-onglet.")) return;
        t.SubTabs.Remove(s);
        var dest = t.SubTabs[0];
        foreach (var item in s.Items) dest.Items.Add(item);
        t.SelectedSubTabIndex = 0;
        f.NotifyChanged();
    }

    // ---------- Raccourcis ----------

    public static (FolderModel? Folder, TabModel? Tab, SubTabModel? Sub) Find(ShortcutItem item)
    {
        foreach (var f in App.Config.Folders)
            foreach (var t in f.Tabs)
                foreach (var s in t.SubTabs)
                    if (s.Items.Contains(item)) return (f, t, s);
        return (null, null, null);
    }

    /// <summary>Déplace un raccourci vers <paramref name="dest"/> en l'insérant avant l'index donné (-1 = à la fin).</summary>
    public static void MoveItem(ShortcutItem item, FolderModel destFolder, SubTabModel dest, int index)
    {
        var (srcFolder, _, src) = Find(item);
        if (src == null) return;
        int old = src.Items.IndexOf(item);

        if (src == dest)
        {
            int target = index < 0 || index > dest.Items.Count ? dest.Items.Count : index;
            if (old < target) target--;
            target = System.Math.Clamp(target, 0, dest.Items.Count - 1);
            if (target != old) dest.Items.Move(old, target);
        }
        else
        {
            src.Items.RemoveAt(old);
            if (index < 0 || index > dest.Items.Count) dest.Items.Add(item);
            else dest.Items.Insert(index, item);
        }
        srcFolder?.NotifyChanged();
        if (destFolder != srcFolder) destFolder.NotifyChanged();
    }

    public static void RenameItem(ShortcutItem item)
    {
        var name = Dialogs.Ask("Nom du raccourci", item.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        item.Name = name.Trim();
        ConfigStore.RequestSave();
    }

    /// <summary>Remet le raccourci sur le bureau (si déplacé) et le retire du dossier.</summary>
    public static void RemoveItem(ShortcutItem item)
    {
        var (f, _, s) = Find(item);
        if (s == null) return;
        if (item.Owned && !ShortcutStorage.Restore(item))
        {
            Dialogs.Info($"Impossible de remettre « {item.Name} » sur le bureau. Voir error.log.");
            return;
        }
        s.Items.Remove(item);
        f?.NotifyChanged();
    }

    /// <summary>Appelé après un glisser vers l'extérieur : si l'Explorateur a déplacé le fichier, on retire l'élément.</summary>
    public static void ForgetIfMovedAway(ShortcutItem item)
    {
        if (!item.Owned || System.IO.File.Exists(item.Path) || System.IO.Directory.Exists(item.Path)) return;
        var (f, _, s) = Find(item);
        s?.Items.Remove(item);
        ShortcutStorage.CleanupDir(item);
        f?.NotifyChanged();
    }
}
