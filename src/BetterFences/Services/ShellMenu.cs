using System;
using Microsoft.Win32;

namespace BetterFences.Services;

/// <summary>
/// Entrées du menu contextuel de l'Explorateur (HKCU, sans droits admin) :
/// clic droit sur le fond du bureau → « Nouveau dossier BetterFences »,
/// clic droit sur un dossier Windows → « Importer dans BetterFences ».
/// Sous Windows 11, elles se trouvent dans « Afficher plus d'options ».
/// </summary>
public static class ShellMenu
{
    private const string DesktopKey = @"Software\Classes\DesktopBackground\Shell\BetterFences.NewFolder";
    private const string FolderKey = @"Software\Classes\Directory\shell\BetterFences.Import";

    public const string NewFolderArg = "--new-folder";
    public const string ImportFolderArg = "--import-folder";

    public static bool IsRegistered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
            return key != null;
        }
    }

    public static void Register()
    {
        try
        {
            var exe = Environment.ProcessPath!;
            Write(DesktopKey, "Nouveau dossier BetterFences", exe, $"\"{exe}\" {NewFolderArg}");
            Write(FolderKey, "Importer dans BetterFences", exe, $"\"{exe}\" {ImportFolderArg} \"%1\"");
        }
        catch (Exception ex)
        {
            Log.Error("Ajout au menu contextuel impossible", ex);
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(DesktopKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(FolderKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Log.Error("Retrait du menu contextuel impossible", ex);
        }
    }

    private static void Write(string path, string label, string exe, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue("MUIVerb", label);
        key.SetValue("Icon", $"\"{exe}\",0");
        using var cmd = key.CreateSubKey("command");
        cmd.SetValue("", command);
    }
}
