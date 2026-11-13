using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BetterFences.Models;
using BetterFences.Services;

namespace BetterFences.Views;

/// <summary>Fenêtre principale : gestion des dossiers, imports/exports, paramètres.</summary>
public partial class ManagerWindow : Window
{
    public const string RepoUrl = "https://github.com/polo5922/BetterFences";

    /// <summary>Une ligne de la liste des dossiers.</summary>
    public sealed class FolderRow
    {
        public FolderRow(FolderModel f)
        {
            Folder = f;
            int tabs = f.Tabs.Count, items = f.AllItems.Count();
            Summary = $"{(f.Mode == FolderMode.Icon ? "Icône" : "Panneau")} · {tabs} onglet{(tabs > 1 ? "s" : "")} · {items} raccourci{(items > 1 ? "s" : "")}";
        }
        public FolderModel Folder { get; }
        public string Summary { get; }
        public string IconTag => Folder.Mode == FolderMode.Icon ? "On" : "";
        public string PanelTag => Folder.Mode == FolderMode.Panel ? "On" : "";
    }

    private bool _refreshing;

    public ManagerWindow()
    {
        InitializeComponent();
        var logo = LoadLogo();
        Icon = logo;
        Logo.Source = logo;
        AboutLogo.Source = logo;
        var v = typeof(App).Assembly.GetName().Version;
        var version = v == null ? "" : $"Version {v.Major}.{v.Minor}.{v.Build}";
        VersionText.Text = version;
        AboutVersion.Text = version + " · dossiers d'applis pour le bureau";

        if (App.IsTestInstance)
        {
            // Une instance de test ne touche ni au registre ni au planificateur de tâches.
            StartupSwitch.IsEnabled = false;
            ShellMenuSwitch.IsEnabled = false;
        }

        SourceInitialized += (_, _) => ApplyWindowTheme();
        StateChanged += (_, _) =>
        {
            // Avec WindowChrome, une fenêtre agrandie déborde de l'écran de l'épaisseur du cadre.
            ((FrameworkElement)Content).Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
        };
        Activated += (_, _) => Refresh();
        ShowPage(NavFolders);
    }

    // ---------- Navigation ----------

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (IsInitialized) ShowPage((RadioButton)sender);
    }

    private void ShowPage(RadioButton nav)
    {
        if (FoldersPage == null) return;
        FoldersPage.Visibility = nav == NavFolders ? Visibility.Visible : Visibility.Collapsed;
        TransferPage.Visibility = nav == NavTransfer ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = nav == NavSettings ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = nav == NavAbout ? Visibility.Visible : Visibility.Collapsed;
        NewFolderButton.Visibility = nav == NavFolders ? Visibility.Visible : Visibility.Collapsed;
        (PageTitle.Text, PageSubtitle.Text) =
            nav == NavFolders ? ("Dossiers", "Vos dossiers du bureau. Glissez des raccourcis dessus pour les remplir.")
            : nav == NavTransfer ? ("Importer et exporter", "Récupérez vos fences, un dossier Windows, ou sauvegardez votre configuration.")
            : nav == NavSettings ? ("Paramètres", "Démarrage, menu contextuel et comportement des dossiers.")
            : ("À propos", "");
        Refresh();
    }

    public void ShowFoldersPage() => NavFolders.IsChecked = true;

    // ---------- Rafraîchissement ----------

    public void Refresh()
    {
        if (FolderList == null) return;
        FolderList.ItemsSource = App.Config.Folders.Select(f => new FolderRow(f)).ToList();
        EmptyState.Visibility = App.Config.Folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _refreshing = true;
        LockSwitch.IsChecked = App.Config.Locked;
        HideSwitch.IsChecked = App.Instance.FoldersHidden;
        ShellMenuSwitch.IsChecked = !App.IsTestInstance && ShellMenu.IsRegistered;
        _refreshing = false;
        if (!App.IsTestInstance) RefreshStartupAsync();
    }

    private async void RefreshStartupAsync()
    {
        // schtasks prend une fraction de seconde : on ne bloque pas l'interface.
        bool enabled = await Task.Run(() => StartupService.IsEnabled);
        _refreshing = true;
        StartupSwitch.IsChecked = enabled;
        _refreshing = false;
    }

    // ---------- Dossiers ----------

    private static FolderModel FolderOf(object sender) => ((FolderRow)((FrameworkElement)sender).DataContext).Folder;

    private void OnNewFolder(object sender, RoutedEventArgs e)
    {
        var f = FolderActions.NewFolder();
        if (App.Instance.FoldersHidden) App.Instance.SetFoldersHidden(false);
        Refresh();
        FolderActions.RenameFolder(f);
        Refresh();
    }

    private void OnModeIcon(object sender, RoutedEventArgs e) { FolderActions.SetMode(FolderOf(sender), FolderMode.Icon); Refresh(); }
    private void OnModePanel(object sender, RoutedEventArgs e) { FolderActions.SetMode(FolderOf(sender), FolderMode.Panel); Refresh(); }

    private void OnRename(object sender, RoutedEventArgs e) { FolderActions.RenameFolder(FolderOf(sender)); Refresh(); }

    private void OnDelete(object sender, RoutedEventArgs e) { FolderActions.DeleteFolder(FolderOf(sender)); Refresh(); }

    private void OnRecenter(object sender, RoutedEventArgs e)
    {
        var f = FolderOf(sender);
        var wa = SystemParameters.WorkArea;
        double w = f.Mode == FolderMode.Panel ? f.Width : 110, h = f.Mode == FolderMode.Panel ? f.Height : 130;
        f.X = wa.Left + (wa.Width - w) / 2;
        f.Y = wa.Top + (wa.Height - h) / 2;
        if (App.Instance.FoldersHidden) App.Instance.SetFoldersHidden(false);
        App.Instance.ShowFolder(f);
        ConfigStore.RequestSave();
    }

    private void OnColor(object sender, RoutedEventArgs e)
    {
        var f = FolderOf(sender);
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender };
        foreach (var (name, hex) in FolderActions.Colors)
        {
            var item = new MenuItem
            {
                Header = name,
                IsChecked = string.Equals(f.AccentColor, hex, StringComparison.OrdinalIgnoreCase),
                Icon = new System.Windows.Shapes.Ellipse
                {
                    Width = 12, Height = 12,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                },
            };
            item.Click += (_, _) => { FolderActions.SetColor(f, hex); Refresh(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    // ---------- Importer et exporter ----------

    private void OnImportFences(object sender, RoutedEventArgs e) { App.Instance.ImportFences(); AfterImport(); }
    private void OnImportFolder(object sender, RoutedEventArgs e) { App.Instance.ImportWindowsFolder(); AfterImport(); }
    private void OnExport(object sender, RoutedEventArgs e) => ConfigTransfer.Export();
    private void OnImportConfig(object sender, RoutedEventArgs e) { ConfigTransfer.Import(); AfterImport(); }

    private void AfterImport()
    {
        Refresh();
        Activate();
    }

    private void OnOpenData(object sender, RoutedEventArgs e) => OpenShell(ConfigStore.DataDir);

    // ---------- Paramètres ----------

    private void OnStartupToggle(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        bool want = StartupSwitch.IsChecked == true;
        if (!StartupService.SetEnabled(want))
            Dialogs.Info("Le lancement avec Windows n'a pas pu être modifié. Détails dans error.log.");
        Refresh();
    }

    private void OnShellMenuToggle(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        // Écrit dans HKLM : Windows demande l'autorisation administrateur.
        bool ok = ShellMenuSwitch.IsChecked == true ? ShellMenu.Register() : ShellMenu.Unregister();
        if (!ok)
            Dialogs.Info("Le menu contextuel n'a pas été modifié (autorisation administrateur refusée ou impossible).");
        App.Config.ShellMenu = ShellMenu.IsRegistered;
        ConfigStore.RequestSave();
        Refresh();
    }

    private void OnLockToggle(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        App.Config.Locked = LockSwitch.IsChecked == true;
        ConfigStore.RequestSave();
        App.Instance.RebuildWindows(); // met à jour les poignées de redimensionnement
        Activate();
    }

    private void OnHideToggle(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        App.Instance.SetFoldersHidden(HideSwitch.IsChecked == true);
    }

    // ---------- À propos ----------

    private void OnGitHub(object sender, RoutedEventArgs e) => OpenShell(RepoUrl);

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        var log = Path.Combine(ConfigStore.DataDir, "error.log");
        if (File.Exists(log)) OpenShell(log);
        else Dialogs.Info("Aucune erreur enregistrée.");
    }

    private void OnQuit(object sender, RoutedEventArgs e) => App.Instance.Shutdown();

    // ---------- Barre de titre ----------

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // ---------- Utilitaires ----------

    private static void OpenShell(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("Ouverture de " + target, ex); }
    }

    private static BitmapSource LoadLogo()
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream("BetterFences.ico")!;
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Barre de titre sombre et coins arrondis (Windows 11).</summary>
    private void ApplyWindowTheme()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1, round = 2;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));   // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));  // DWMWA_WINDOW_CORNER_PREFERENCE = arrondi
    }
}
