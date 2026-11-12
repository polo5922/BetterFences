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
    private readonly ToolStripMenuItem _lock, _hide, _startup;

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
        _startup = new ToolStripMenuItem("Lancer avec Windows", null, (_, _) => ToggleStartup());
        menu.Items.Add(_startup);
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
