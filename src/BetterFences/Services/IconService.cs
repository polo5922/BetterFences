using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BetterFences.Interop;
using BetterFences.Models;
using static BetterFences.Interop.NativeMethods;

namespace BetterFences.Services;

/// <summary>Récupère les icônes haute résolution via le Shell, chargées progressivement sans bloquer l'UI.</summary>
public static class IconService
{
    private const int IconSize = 128;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<ShortcutItem> Pending = new();
    private static bool _scheduled;

    public static void Queue(ShortcutItem item)
    {
        Pending.Enqueue(item);
        Schedule();
    }

    public static void Invalidate(string path) => Cache.Remove(path);

    private static void Schedule()
    {
        if (_scheduled) return;
        _scheduled = true;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, ProcessQueue);
    }

    private static void ProcessQueue()
    {
        _scheduled = false;
        var sw = Stopwatch.StartNew();
        while (Pending.Count > 0 && sw.ElapsedMilliseconds < 40)
        {
            var item = Pending.Dequeue();
            // Cible disparue après une mise à jour de l'appli : on recâble le raccourci avant de lire l'icône.
            if (ShortcutRepair.TryRepair(item)) Invalidate(item.Path);
            item.SetIcon(Get(item.Path));
        }
        if (Pending.Count > 0) Schedule();
    }

    public static ImageSource? Get(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;
        ImageSource? img = null;
        try { img = IconSource.Load(path); } catch { /* repli ci-dessous */ }
        if (img == null)
        {
            // Raccourci sans chemin (appli du Store, ex. Xbox) : icône de la cible elle-même.
            try { img = FromLinkTarget(path, IconSize); } catch { /* repli ci-dessous */ }
        }
        if (img == null)
        {
            try { img = FromShellItem(path, IconSize); } catch { /* repli ci-dessous */ }
        }
        if (img == null)
        {
            try { img = FromFileInfo(path); } catch { /* pas d'icône */ }
        }
        Cache[path] = img;
        return img;
    }

    /// <summary>Icône de la cible d'un raccourci qui n'a pas de chemin de fichier (liste d'identifiants du Shell).</summary>
    private static ImageSource? FromLinkTarget(string path, int size)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        var link = (IShellLinkW)new CShellLink();
        IntPtr pidl = IntPtr.Zero;
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var sb = new StringBuilder(1024);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            if (sb.Length > 0) return null; // cible fichier : géré par IconSource / le Shell
            link.GetIDList(out pidl);
            if (pidl == IntPtr.Zero) return null;

            // Appli du Store : la cible est « IdentifiantPaquet!Appli ». Le Shell ne rend son logo
            // que depuis shell:AppsFolder (via le raccourci, il renvoie une icône de fichier vide).
            if (ShellLinkNative.SHGetNameFromIDList(pidl, ShellLinkNative.SIGDN_PARENTRELATIVEPARSING, out var namePtr) == 0)
            {
                string name = Marshal.PtrToStringUni(namePtr) ?? "";
                ShellLinkNative.CoTaskMemFree(namePtr);
                if (name.Contains('!'))
                {
                    try { return FromShellItem(@"shell:AppsFolder\" + name, size); }
                    catch { /* repli ci-dessous */ }
                }
            }
            ShellLinkNative.SHCreateItemFromIDList(pidl, typeof(IShellItemImageFactory).GUID, out var factory);
            return FromFactory(factory, size);
        }
        finally
        {
            if (pidl != IntPtr.Zero) ShellLinkNative.CoTaskMemFree(pidl);
            Marshal.ReleaseComObject(link);
        }
    }

    private static ImageSource? FromShellItem(string path, int size)
    {
        SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factory);
        return FromFactory(factory, size);
    }

    private static ImageSource? FromFactory(IShellItemImageFactory factory, int size)
    {
        try
        {
            int hr = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF.BiggerSizeOk | SIIGBF.IconOnly, out var hbm);
            if (hr != 0 || hbm == IntPtr.Zero) return null;
            try { return FromHBitmap(hbm); }
            finally { DeleteObject(hbm); }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>Convertit un HBITMAP 32 bits en conservant la transparence (CreateBitmapSourceFromHBitmap la perd).</summary>
    private static BitmapSource? FromHBitmap(IntPtr hbm)
    {
        if (GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bm) == 0) return null;
        int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
        if (w <= 0 || h <= 0) return null;

        var bmi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h, // top-down
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };
        var pixels = new byte[w * h * 4];
        var hdc = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(hdc, hbm, 0, (uint)h, pixels, ref bmi, 0) == 0) return null;
        }
        finally { ReleaseDC(IntPtr.Zero, hdc); }

        // Certaines icônes n'ont pas de canal alpha : on les rend opaques.
        bool hasAlpha = false;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) { hasAlpha = true; break; }
        if (!hasAlpha) for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;

        var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
        src.Freeze();
        return src;
    }

    private static ImageSource? FromFileInfo(string path)
    {
        var info = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
        if (info.hIcon == IntPtr.Zero) return null;
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally { DestroyIcon(info.hIcon); }
    }
}
