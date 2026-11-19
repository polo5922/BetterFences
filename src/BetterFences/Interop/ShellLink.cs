using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterFences.Interop;

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
internal class CShellLink { }

/// <summary>IShellLinkW complet (l'ordre des méthodes doit suivre la vtable).</summary>
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
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
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRel, uint reserved);
    void Resolve(IntPtr hwnd, uint flags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

internal static class ShellLinkNative
{
    /// <summary>Crée un IShellItemImageFactory à partir d'une liste d'identifiants (cible d'un raccourci sans chemin, ex. appli du Store).</summary>
    [DllImport("shell32.dll", PreserveSig = false)]
    public static extern void SHCreateItemFromIDList(
        IntPtr pidl,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out NativeMethods.IShellItemImageFactory ppv);

    public const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;

    [DllImport("shell32.dll")]
    public static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdn, out IntPtr name);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr pv);
}
