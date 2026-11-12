using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace BetterFences.Services;

/// <summary>
/// Entrées du menu contextuel de l'Explorateur :
/// clic droit sur le fond du bureau → « Nouveau dossier BetterFences »,
/// clic droit sur un dossier Windows → « Importer dans BetterFences ».
/// Elles sont écrites dans HKLM : sur certaines machines, l'Explorateur ignore les verbes
/// déclarés seulement pour l'utilisateur (HKCU). L'écriture demande donc les droits
/// administrateur ; l'appli se relance élevée avec <see cref="RegisterArg"/> / <see cref="UnregisterArg"/>.
/// </summary>
public static class ShellMenu
{
    private const string DesktopKey = @"Software\Classes\DesktopBackground\Shell\BetterFences.NewFolder";
    private const string FolderKey = @"Software\Classes\Directory\shell\BetterFences.Import";

    public const string NewFolderArg = "--new-folder";
    public const string ImportFolderArg = "--import-folder";
    public const string RegisterArg = "--register-shell-menu";
    public const string UnregisterArg = "--unregister-shell-menu";

    public static bool IsRegistered
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(DesktopKey);
            return key != null;
        }
    }

    /// <summary>Ajoute les entrées (demande l'élévation). Renvoie true si elles sont en place.</summary>
    public static bool Register() => RunElevated(RegisterArg) && IsRegistered;

    /// <summary>Retire les entrées (demande l'élévation). Renvoie true si elles ont disparu.</summary>
    public static bool Unregister() => RunElevated(UnregisterArg) && !IsRegistered;

    /// <summary>
    /// Traite --register-shell-menu / --unregister-shell-menu dans le processus élevé.
    /// Renvoie le code de sortie, ou null si les arguments ne concernent pas le menu.
    /// </summary>
    public static int? HandleElevatedArgs(string[] args)
    {
        if (args.Length == 0) return null;
        try
        {
            if (args[0] == RegisterArg)
            {
                var exe = Environment.ProcessPath!;
                Write(DesktopKey, "Nouveau dossier BetterFences", exe, $"\"{exe}\" {NewFolderArg}");
                Write(FolderKey, "Importer dans BetterFences", exe, $"\"{exe}\" {ImportFolderArg} \"%1\"");
                return 0;
            }
            if (args[0] == UnregisterArg)
            {
                Registry.LocalMachine.DeleteSubKeyTree(DesktopKey, throwOnMissingSubKey: false);
                Registry.LocalMachine.DeleteSubKeyTree(FolderKey, throwOnMissingSubKey: false);
                return 0;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Modification du menu contextuel impossible", ex);
            return 1;
        }
        return null;
    }

    /// <summary>Retire les anciennes entrées HKCU de la version 1.1.0 (ignorées par l'Explorateur).</summary>
    public static void RemoveLegacyUserKeys()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(DesktopKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(FolderKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Log.Error("Nettoyage des anciennes entrées du menu contextuel impossible", ex);
        }
    }

    private static bool RunElevated(string arg)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, arg)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (p == null) return false;
            p.WaitForExit(30_000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // élévation refusée par l'utilisateur
        }
        catch (Exception ex)
        {
            Log.Error("Lancement élevé impossible", ex);
            return false;
        }
    }

    private static void Write(string path, string label, string exe, string command)
    {
        using var key = Registry.LocalMachine.CreateSubKey(path);
        key.SetValue("MUIVerb", label);
        key.SetValue("Icon", $"\"{exe}\",0");
        using var cmd = key.CreateSubKey("command");
        cmd.SetValue("", command);
    }
}
