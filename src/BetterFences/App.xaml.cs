using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BetterFences.Models;
using BetterFences.Services;
using BetterFences.Views;

namespace BetterFences;

public partial class App : Application
{
    public static App Instance { get; private set; } = null!;
    public static AppConfig Config { get; private set; } = new();

    private Mutex? _mutex;
    private TrayIcon? _tray;
    private readonly Dictionary<string, Window> _windows = new();
    private PopupWindow? _popup;
    private string? _lastPopupFolderId;
    private DateTime _lastPopupClose;
    private bool _hidden;
    private DispatcherTimer? _rebuildTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        Instance = this;
        // Une instance par dossier de données (BETTERFENCES_DATA permet une instance de test à côté).
        var mutexName = @"Local\BetterFences.SingleInstance";
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BETTERFENCES_DATA")))
            mutexName += "." + (uint)ConfigStore.DataDir.ToLowerInvariant().GetHashCode();
        _mutex = new Mutex(true, mutexName, out bool first);
        if (!first)
        {
            MessageBox.Show("BetterFences est déjà lancé (icône dans la zone de notification).", "BetterFences",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Exception non gérée", args.Exception);
            args.Handled = true;
        };

        bool firstRun = !ConfigStore.ConfigExists;
        Config = ConfigStore.Load();
        foreach (var f in Config.Folders) f.EnsureValid();

        _tray = new TrayIcon(this);
        foreach (var f in Config.Folders) ShowFolder(f);

        if (Snapshot.Directory != null) Snapshot.Run(this);
        else if (firstRun) Dispatcher.BeginInvoke(FirstRun, DispatcherPriority.ApplicationIdle);
    }

    private void FirstRun()
    {
        if (FencesImporter.FindLatestSnapshot() != null &&
            Dialogs.Confirm("Bienvenue dans BetterFences !\n\nUne configuration Fences a été trouvée. Voulez-vous importer vos fences sous forme de dossiers ?"))
        {
            ImportFences();
        }
        if (Config.Folders.Count == 0)
        {
            var f = FolderActions.NewFolder();
            f.Name = "Mes applis";
            f.Mode = FolderMode.Panel;
            ShowFolder(f);
        }
        ConfigStore.SaveNow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { ConfigStore.SaveNow(); } catch (Exception ex) { Log.Error("Sauvegarde à la fermeture", ex); }
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ---------- Fenêtres de dossiers ----------

    public void ShowFolder(FolderModel f)
    {
        if (_windows.Remove(f.Id, out var old)) CloseWindow(old);
        DesktopWindow w = f.Mode == FolderMode.Icon ? new FolderIconWindow(f) : new FolderPanelWindow(f);
        _windows[f.Id] = w;
        if (!_hidden) w.Show();
    }

    public void CloseFolderWindow(FolderModel f)
    {
        if (_windows.Remove(f.Id, out var w)) CloseWindow(w);
    }

    private static void CloseWindow(Window w)
    {
        if (w is DesktopWindow d) d.ClosingByApp = true;
        w.Close();
    }

    public bool FoldersHidden => _hidden;

    public void SetFoldersHidden(bool hidden)
    {
        _hidden = hidden;
        ClosePopup();
        foreach (var w in _windows.Values)
        {
            if (hidden) w.Hide(); else w.Show();
        }
    }

    /// <summary>Recrée toutes les fenêtres (après un redémarrage de l'Explorateur, Progman a changé).</summary>
    public void RebuildWindowsSoon()
    {
        _rebuildTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Background, (_, _) =>
        {
            _rebuildTimer!.Stop();
            RebuildWindows();
        }, Dispatcher);
        _rebuildTimer.Stop();
        _rebuildTimer.Start();
    }

    public void RebuildWindows()
    {
        ClosePopup();
        foreach (var w in _windows.Values.ToList()) CloseWindow(w);
        _windows.Clear();
        foreach (var f in Config.Folders) ShowFolder(f);
    }

    // ---------- Popup (mode icône) ----------

    public void TogglePopup(FolderModel f, FolderIconWindow anchor)
    {
        // Un clic sur la tuile ferme d'abord la popup (perte de focus) : on ne la rouvre pas aussitôt.
        if (_popup == null && _lastPopupFolderId == f.Id && (DateTime.Now - _lastPopupClose).TotalMilliseconds < 300)
            return;
        if (_popup != null)
        {
            bool same = _popup.Folder == f;
            ClosePopup();
            if (same) return;
        }

        var popup = new PopupWindow(f, anchor.TileScreenRect);
        popup.Closed += (_, _) =>
        {
            if (_popup == popup) _popup = null;
            _lastPopupFolderId = f.Id;
            _lastPopupClose = DateTime.Now;
        };
        _popup = popup;
        popup.Show();
    }

    public void ClosePopup() => _popup?.SafeClose();

    /// <summary>Après une boîte de dialogue, rend le focus à la popup pour qu'elle se ferme normalement ensuite.</summary>
    public void ReactivatePopup() => _popup?.Activate();

    // ---------- Imports ----------

    public void ImportFences()
    {
        var snapshot = FencesImporter.FindLatestSnapshot();
        if (snapshot == null)
        {
            Dialogs.Info("Aucune configuration Fences trouvée.");
            return;
        }

        var choice = Dialogs.YesNoCancel(
            "Importer depuis Fences.\n\n" +
            "Oui : déplacer les raccourcis du bureau dans BetterFences (comme Fences, ils disparaissent du bureau).\n" +
            "Non : garder les fichiers en place et créer seulement des liens vers eux.\n\n" +
            "Pensez à quitter Fences ensuite pour éviter que les deux se superposent.");
        if (choice == null) return;

        try
        {
            var folders = FencesImporter.Import(snapshot, choice == true, out int count);
            foreach (var f in folders)
            {
                f.EnsureValid();
                Config.Folders.Add(f);
                ShowFolder(f);
            }
            ConfigStore.SaveNow();
            Dialogs.Info($"{folders.Count} dossier(s) et {count} raccourci(s) importés.");
        }
        catch (Exception ex)
        {
            Log.Error("Import Fences", ex);
            Dialogs.Info("L'import a échoué : " + ex.Message);
        }
    }

    public void ImportWindowsFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choisir un dossier à importer" };
        if (dlg.ShowDialog() != true) return;
        var dir = dlg.FolderName;

        var folder = new FolderModel
        {
            Name = Path.GetFileName(dir.TrimEnd('\\')) is { Length: > 0 } n ? n : dir,
            Mode = FolderMode.Panel,
            X = SystemParameters.WorkArea.Left + 80,
            Y = SystemParameters.WorkArea.Top + 80,
        };
        var tab = new TabModel("Général");
        folder.Tabs.Add(tab);
        var entries = Directory.EnumerateFileSystemEntries(dir)
            .Where(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase);
        foreach (var p in entries)
        {
            var item = ShortcutStorage.Import(p, move: false);
            if (item != null) tab.SubTabs[0].Items.Add(item);
        }
        folder.EnsureValid();
        Config.Folders.Add(folder);
        ShowFolder(folder);
        ConfigStore.SaveNow();
    }
}
