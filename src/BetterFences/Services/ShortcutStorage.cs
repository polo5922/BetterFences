using System;
using System.IO;
using System.Linq;
using BetterFences.Models;

namespace BetterFences.Services;

/// <summary>
/// Gère le stockage caché des raccourcis : un raccourci (.lnk, .url…) déposé dans un dossier est
/// déplacé dans %AppData%\BetterFences\Items\{id}\ (il disparaît donc du bureau). Les autres fichiers
/// (exe, documents, dossiers) sont seulement référencés, jamais déplacés. Aucun fichier n'est supprimé.
/// </summary>
public static class ShortcutStorage
{
    public static string ItemsDir => Path.Combine(ConfigStore.DataDir, "Items");

    private static readonly string[] ShortcutExtensions = { ".lnk", ".url", ".appref-ms", ".website", ".pif" };

    public static bool IsShortcut(string path)
        => ShortcutExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static ShortcutItem? Import(string source, bool move)
    {
        bool isDir = Directory.Exists(source);
        if (!isDir && !File.Exists(source)) return null;

        var item = new ShortcutItem
        {
            Name = isDir ? new DirectoryInfo(source).Name : Path.GetFileNameWithoutExtension(source),
            Path = source,
        };

        // Déjà dans notre stockage (ex. glissé depuis l'Explorateur) : on le reprend tel quel.
        if (source.StartsWith(ItemsDir, StringComparison.OrdinalIgnoreCase))
        {
            item.Owned = true;
            item.OriginalPath = Path.Combine(DesktopDir, Path.GetFileName(source));
            return item;
        }

        if (move && !isDir && IsShortcut(source))
        {
            var dir = Path.Combine(ItemsDir, item.Id);
            try
            {
                Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, Path.GetFileName(source));
                File.Move(source, dest);
                item.Path = dest;
                item.OriginalPath = source;
                item.Owned = true;
            }
            catch (Exception ex)
            {
                // Ex. bureau public (C:\Users\Public\Desktop) qui exige les droits admin : on garde une référence.
                Log.Error($"Déplacement impossible de {source}, conservé comme référence", ex);
                TryDeleteEmptyDir(dir);
            }
        }
        return item;
    }

    /// <summary>Remet un raccourci possédé sur le bureau (ou à son emplacement d'origine).</summary>
    public static bool Restore(ShortcutItem item)
    {
        if (!item.Owned || !File.Exists(item.Path)) return !item.Owned;
        try
        {
            string fileName = Path.GetFileName(item.Path);
            string? targetDir = item.OriginalPath != null ? Path.GetDirectoryName(item.OriginalPath) : null;
            if (targetDir == null || !Directory.Exists(targetDir)) targetDir = DesktopDir;

            string dest = UniquePath(Path.Combine(targetDir, fileName));
            try
            {
                File.Move(item.Path, dest);
            }
            catch (UnauthorizedAccessException)
            {
                dest = UniquePath(Path.Combine(DesktopDir, fileName));
                File.Move(item.Path, dest);
            }
            CleanupDir(item);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Restauration impossible de {item.Path}", ex);
            return false;
        }
    }

    public static void CleanupDir(ShortcutItem item)
    {
        if (!item.Owned) return;
        TryDeleteEmptyDir(Path.Combine(ItemsDir, item.Id));
    }

    private static void TryDeleteEmptyDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { /* ignoré */ }
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
