using System;
using System.Drawing;
using System.Windows.Forms;

namespace BetterFences.Services;

/// <summary>Icône de la zone de notification et son menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly App _app;
    private readonly ToolStripMenuItem _hide;

    public TrayIcon(App app)
    {
        _app = app;
        // Tout se règle dans la fenêtre de gestion ; le menu garde seulement l'essentiel.
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        var open = new ToolStripMenuItem("Ouvrir BetterFences", null, (_, _) => _app.ShowManager());
        open.Font = new Font(open.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(open);
        menu.Items.Add("Nouveau dossier", null, (_, _) => FolderActions.NewFolder());
        _hide = new ToolStripMenuItem("Masquer les dossiers", null, (_, _) => _app.SetFoldersHidden(!_app.FoldersHidden));
        menu.Items.Add(_hide);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => _app.Shutdown());
        menu.Opening += (_, _) => _hide.Text = _app.FoldersHidden ? "Afficher les dossiers" : "Masquer les dossiers";

        _icon = new NotifyIcon
        {
            Text = "BetterFences",
            Icon = CreateAppIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _app.ShowManager(); };
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
