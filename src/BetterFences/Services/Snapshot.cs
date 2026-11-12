using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BetterFences.Models;
using BetterFences.Views;

namespace BetterFences.Services;

/// <summary>
/// Outil de documentation : avec la variable BETTERFENCES_SNAPSHOT=&lt;dossier&gt;, l'appli enregistre chaque
/// fenêtre en PNG transparent (puis ouvre la popup du premier dossier en mode icône), et se ferme.
/// </summary>
internal static class Snapshot
{
    public static string? Directory => Environment.GetEnvironmentVariable("BETTERFENCES_SNAPSHOT");

    public static void Run(App app)
    {
        var dir = Directory!;
        System.IO.Directory.CreateDirectory(dir);
        After(2.5, () =>
        {
            SaveAll(dir, "");
            var iconWin = app.Windows.OfType<FolderIconWindow>().FirstOrDefault();
            var folder = App.Config.Folders.FirstOrDefault(f => f.Mode == FolderMode.Icon);
            if (iconWin != null && folder != null) app.TogglePopup(folder, iconWin);
            After(2.0, () =>
            {
                foreach (var p in app.Windows.OfType<PopupWindow>()) Save(p, Path.Combine(dir, "popup.png"));
                app.Shutdown();
            });
        });
    }

    private static void SaveAll(string dir, string prefix)
    {
        int n = 0;
        foreach (var w in Application.Current.Windows.OfType<DesktopWindow>())
            Save(w, Path.Combine(dir, $"{prefix}{(w is FolderIconWindow ? "icon" : "panel")}-{n++}.png"));
    }

    private static void Save(Window w, string file)
    {
        if (w.ActualWidth < 1) return;
        var dpi = VisualTreeHelper.GetDpi(w);
        var bmp = new RenderTargetBitmap(
            (int)Math.Ceiling(w.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(w.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(w);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(file);
        enc.Save(fs);
    }

    private static void After(double seconds, Action action)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        t.Tick += (_, _) => { t.Stop(); action(); };
        t.Start();
    }
}
