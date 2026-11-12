using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace BetterFences.Services;

/// <summary>Icône de la zone de notification et son menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly App _app;
    private readonly ToolStripMenuItem _lock, _hide, _startup, _shellMenu;

    public TrayIcon(App app)
    {
        _app = app;
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("Nouveau dossier", null, (_, _) => FolderActions.NewFolder());
        menu.Items.Add(new ToolStripSeparator());
        _lock = new ToolStripMenuItem("Verrouiller les positions", null, (_, _) => ToggleLock());
        _hide = new ToolStripMenuItem("Masquer les dossiers", null, (_, _) => ToggleHidden());
        menu.Items.Add(_lock);
        menu.Items.Add(_hide);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Importer depuis Fences…", null, (_, _) => _app.ImportFences());
        menu.Items.Add("Importer un dossier Windows…", null, (_, _) => _app.ImportWindowsFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exporter ma configuration…", null, (_, _) => ConfigTransfer.Export());
        menu.Items.Add("Importer une configuration…", null, (_, _) => ConfigTransfer.Import());
        menu.Items.Add(new ToolStripSeparator());
        _startup = new ToolStripMenuItem("Lancer avec Windows", null, (_, _) => ToggleStartup());
        menu.Items.Add(_startup);
        _shellMenu = new ToolStripMenuItem("Menu contextuel du bureau", null, (_, _) => ToggleShellMenu())
        {
            ToolTipText = "Clic droit sur le bureau : Nouveau dossier BetterFences\nClic droit sur un dossier : Importer dans BetterFences\n(demande les droits administrateur)",
        };
        menu.Items.Add(_shellMenu);
        menu.Items.Add("Ouvrir le dossier des données", null, (_, _) =>
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ConfigStore.DataDir}\"") { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => _app.Shutdown());
        menu.Opening += (_, _) => RefreshChecks();

        _icon = new NotifyIcon
        {
            Text = "BetterFences",
            Icon = CreateAppIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleHidden(); };
    }

    private void RefreshChecks()
    {
        _lock.Checked = App.Config.Locked;
        _hide.Text = _app.FoldersHidden ? "Afficher les dossiers" : "Masquer les dossiers";
        _startup.Checked = StartupService.IsEnabled;
        _shellMenu.Checked = ShellMenu.IsRegistered;
    }

    private void ToggleLock()
    {
        App.Config.Locked = !App.Config.Locked;
        ConfigStore.RequestSave();
        _app.RebuildWindows(); // met à jour les poignées de redimensionnement
    }

    private void ToggleHidden() => _app.SetFoldersHidden(!_app.FoldersHidden);

    private static void ToggleStartup()
    {
        try { StartupService.SetEnabled(!StartupService.IsEnabled); }
        catch (Exception ex) { Log.Error("Lancer avec Windows", ex); }
    }

    private static void ToggleShellMenu()
    {
        // Écrit dans HKLM : Windows demande l'autorisation administrateur.
        bool ok = ShellMenu.IsRegistered ? ShellMenu.Unregister() : ShellMenu.Register();
        if (!ok)
            System.Windows.MessageBox.Show(
                "Le menu contextuel n'a pas été modifié (autorisation administrateur refusée ou impossible).",
                "BetterFences", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        App.Config.ShellMenu = ShellMenu.IsRegistered;
        ConfigStore.RequestSave();
    }

    /// <summary>Icône de l'appli (Assets/BetterFences.ico), à la taille des petites icônes du système.</summary>
    private static Icon CreateAppIcon()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("BetterFences.ico")!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
