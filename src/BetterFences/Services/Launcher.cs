using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using BetterFences.Models;

namespace BetterFences.Services;

public static class Launcher
{
    public static void Launch(ShortcutItem item, bool asAdmin = false)
    {
        try
        {
            var psi = new ProcessStartInfo(item.Path) { UseShellExecute = true };
            if (asAdmin) psi.Verb = "runas";
            Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // UAC annulé par l'utilisateur.
        }
        catch (Exception ex)
        {
            Log.Error($"Lancement impossible de {item.Path}", ex);
            Dialogs.Info($"Impossible d'ouvrir « {item.Name} ».\n\n{ex.Message}");
        }
    }

    public static void OpenLocation(ShortcutItem item)
    {
        try
        {
            string target = ResolveTarget(item.Path) ?? item.Path;
            if (Directory.Exists(target))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
            else if (File.Exists(target))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"") { UseShellExecute = true });
            else
                Dialogs.Info("L'emplacement de ce raccourci est introuvable (raccourci web ou cible supprimée).");
        }
        catch (Exception ex)
        {
            Log.Error("Ouverture de l'emplacement impossible", ex);
        }
    }

    /// <summary>Cible d'un .lnk (null si ce n'est pas un .lnk ou si elle est vide).</summary>
    public static string? ResolveTarget(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return null;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(path);
            string target = link.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch { return null; }
    }
}
