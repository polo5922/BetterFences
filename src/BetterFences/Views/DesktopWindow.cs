using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static BetterFences.Interop.NativeMethods;

namespace BetterFences.Views;

/// <summary>
/// Fenêtre « posée sur le bureau » : absente de la barre des tâches et d'Alt+Tab, possédée par Progman
/// (reste visible après Win+D) et toujours maintenue sous les autres fenêtres.
/// </summary>
public class DesktopWindow : Window
{
    private static readonly uint TaskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

    public DesktopWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    /// <summary>true quand la fermeture est demandée par l'appli (sinon : l'Explorateur a redémarré).</summary>
    public bool ClosingByApp { get; set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        MakeToolWindow(hwnd);

        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero) SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, progman);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static void MakeToolWindow(IntPtr hwnd)
    {
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            wp.hwndInsertAfter = HWND_BOTTOM;
            wp.flags &= ~SWP_NOZORDER;
            Marshal.StructureToPtr(wp, lParam, false);
        }
        else if (msg == TaskbarCreatedMsg)
        {
            // L'Explorateur a redémarré : Progman a changé, on recrée les fenêtres.
            App.Instance.RebuildWindowsSoon();
        }
        return IntPtr.Zero;
    }
}
