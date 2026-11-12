using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Media.Imaging;

namespace BetterFences.Services;

/// <summary>
/// Lit l'icône « native » d'un raccourci (plus grande image réellement disponible).
/// Le Shell, lui, place les petites icônes (32 px) dans un cadre gris à la taille « jumbo » ;
/// ici on récupère l'image brute et on laisse WPF la mettre à l'échelle.
/// </summary>
internal static class IconSource
{
    public static BitmapSource? Load(string path)
    {
        var (file, index) = ResolveLocation(path);
        if (string.IsNullOrWhiteSpace(file)) return null;
        file = Environment.ExpandEnvironmentVariables(file.Trim().Trim('"'));
        if (!File.Exists(file)) return null;

        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".exe":
            case ".dll":
            case ".cpl":
            case ".icl":
            case ".ocx":
                var ico = BuildIcoFromResources(file, index);
                return ico == null ? null : Largest(new MemoryStream(ico));
            case ".ico":
            case ".png":
            case ".bmp":
            case ".jpg":
            case ".jpeg":
                using (var s = File.OpenRead(file)) return Largest(s);
            default:
                return null; // document, dossier… → icône du Shell
        }
    }

    // ---------- Emplacement de l'icône ----------

    private static (string? file, int index) ResolveLocation(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".url") return FromUrl(path);
        if (ext == ".lnk") return FromLnk(path);
        if (ext is ".exe" or ".ico") return (path, 0);
        return (null, 0);
    }

    private static (string?, int) FromUrl(string path)
    {
        string? file = null;
        int index = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)) file = line[9..];
            else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase)) int.TryParse(line[10..], out index);
        }
        return (file, index);
    }

    private static (string?, int) FromLnk(string path)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var sb = new StringBuilder(1024);
            link.GetIconLocation(sb, sb.Capacity, out int index);
            if (sb.Length > 0) return (sb.ToString(), index);
            sb.Clear();
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return (sb.Length > 0 ? sb.ToString() : null, 0);
        }
        finally { Marshal.ReleaseComObject(link); }
    }

    // ---------- Décodage ----------

    /// <summary>Décode le fichier (format détecté d'après le contenu, pas l'extension) et garde la plus grande image.</summary>
    private static BitmapSource? Largest(Stream stream)
    {
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapFrame? best = null;
        foreach (var frame in decoder.Frames)
        {
            if (best == null || frame.PixelWidth > best.PixelWidth ||
                (frame.PixelWidth == best.PixelWidth && frame.Format.BitsPerPixel > best.Format.BitsPerPixel))
                best = frame;
        }
        best?.Freeze();
        return best;
    }

    /// <summary>Reconstitue un fichier .ico complet à partir des ressources RT_GROUP_ICON/RT_ICON d'un exécutable.</summary>
    private static byte[]? BuildIcoFromResources(string file, int index)
    {
        var module = LoadLibraryEx(file, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        if (module == IntPtr.Zero) return null;
        try
        {
            byte[]? group = null;
            if (index < 0)
            {
                group = ReadResource(module, (IntPtr)(-index), RT_GROUP_ICON);
            }
            else
            {
                int i = 0;
                byte[]? first = null;
                EnumResourceNames(module, RT_GROUP_ICON, (h, _, name, _) =>
                {
                    var data = ReadResource(h, name, RT_GROUP_ICON);
                    first ??= data;
                    if (i++ == index) { group = data; return false; }
                    return true;
                }, IntPtr.Zero);
                group ??= first;
            }
            if (group == null || group.Length < 6) return null;

            int count = BitConverter.ToUInt16(group, 4);
            var images = new byte[count][];
            for (int k = 0; k < count; k++)
            {
                int e = 6 + k * 14;
                if (e + 14 > group.Length) return null;
                images[k] = ReadResource(module, (IntPtr)BitConverter.ToUInt16(group, e + 12), RT_ICON) ?? Array.Empty<byte>();
            }

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)count);
            int offset = 6 + count * 16;
            for (int k = 0; k < count; k++)
            {
                w.Write(group, 6 + k * 14, 8);         // largeur, hauteur, couleurs, réservé, plans, bits
                w.Write(images[k].Length);
                w.Write(offset);
                offset += images[k].Length;
            }
            foreach (var img in images) w.Write(img);
            return ms.ToArray();
        }
        finally { FreeLibrary(module); }
    }

    private static byte[]? ReadResource(IntPtr module, IntPtr name, IntPtr type)
    {
        var res = FindResource(module, name, type);
        if (res == IntPtr.Zero) return null;
        uint size = SizeofResource(module, res);
        var handle = LoadResource(module, res);
        var ptr = handle == IntPtr.Zero ? IntPtr.Zero : LockResource(handle);
        if (ptr == IntPtr.Zero || size == 0) return null;
        var data = new byte[size];
        Marshal.Copy(ptr, data, 0, (int)size);
        return data;
    }

    // ---------- Interop ----------

    private static readonly IntPtr RT_ICON = 3, RT_GROUP_ICON = 14;
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x2, LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x20;

    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc proc, IntPtr param);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module, IntPtr res);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr data);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr res);

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int cch, out int iconIndex);
    }
}
