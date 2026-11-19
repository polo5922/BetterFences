using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using BetterFences.Interop;
using BetterFences.Models;

namespace BetterFences.Services;

/// <summary>
/// Répare les raccourcis rangés dont la cible a disparu après une mise à jour de l'appli.
/// Certaines applis (Roblox, Discord…) s'installent à chaque mise à jour dans un nouveau dossier
/// (« version-xxxx », « app-1.2.3 ») et suppriment l'ancien : le raccourci rangé dans BetterFences
/// pointe alors dans le vide (plus d'icône, lancement impossible). Leur programme de mise à jour,
/// lui, ne corrige que les raccourcis du menu Démarrer et du bureau.
/// On cherche donc la nouvelle cible : d'abord un raccourci du même nom dans le menu Démarrer
/// ou sur le bureau, sinon le même exécutable dans un dossier « frère » plus récent.
/// L'ancien raccourci est sauvegardé avant modification.
/// </summary>
public static class ShortcutRepair
{
    private const int STGM_READWRITE = 2;
    private static readonly object Gate = new();

    /// <summary>Renvoie true si le raccourci a été mis à jour vers une nouvelle cible.</summary>
    public static bool TryRepair(ShortcutItem item)
    {
        // Appelé au démarrage (en arrière-plan), au chargement des icônes et au lancement.
        lock (Gate) return TryRepairCore(item);
    }

    private static bool TryRepairCore(ShortcutItem item)
    {
        if (!item.Owned || !item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return false;
        // Seulement nos propres copies (Items\) : on ne modifie jamais un raccourci de l'utilisateur ailleurs.
        if (!Path.GetFullPath(item.Path).StartsWith(Path.GetFullPath(ShortcutStorage.ItemsDir) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(item.Path)) return false;

        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)new CShellLink();
            ((IPersistFile)link).Load(item.Path, STGM_READWRITE);
            string oldTarget = Read(link.GetPath);
            if (oldTarget.Length == 0 || File.Exists(oldTarget) || Directory.Exists(oldTarget)) return false;

            string? newTarget = FromOtherShortcuts(item.Path, oldTarget) ?? FromSiblingVersion(oldTarget);
            if (newTarget == null) return false;

            Backup(item);
            string oldDir = Path.GetDirectoryName(oldTarget) ?? "";
            string newDir = Path.GetDirectoryName(newTarget) ?? "";
            link.SetPath(newTarget);

            string workDir = Read(link.GetWorkingDirectory);
            if (workDir.Length > 0 && (SamePath(workDir, oldDir) || !Directory.Exists(workDir)))
                link.SetWorkingDirectory(newDir);

            var sb = new StringBuilder(1024);
            link.GetIconLocation(sb, sb.Capacity, out int iconIndex);
            string icon = sb.ToString();
            if (icon.Length > 0 && !File.Exists(Environment.ExpandEnvironmentVariables(icon)))
            {
                string moved = icon.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase)
                    ? newDir + icon[oldDir.Length..]
                    : newTarget;
                link.SetIconLocation(File.Exists(moved) ? moved : newTarget, File.Exists(moved) ? iconIndex : 0);
            }

            ((IPersistFile)link).Save(null, true);
            Log.Error($"Raccourci « {item.Name} » réparé : {oldTarget} → {newTarget}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Réparation impossible du raccourci {item.Path}", ex);
            return false;
        }
        finally
        {
            if (link != null) Marshal.ReleaseComObject(link);
        }
    }

    // ---------- Recherche de la nouvelle cible ----------

    /// <summary>Raccourci à jour du même nom (ou vers le même exécutable) dans le menu Démarrer ou sur le bureau.</summary>
    private static string? FromOtherShortcuts(string storedLink, string oldTarget)
    {
        string linkName = Path.GetFileName(storedLink);
        string exeName = Path.GetFileName(oldTarget);
        var candidates = new List<(string target, bool sameName)>();

        foreach (var lnk in OtherShortcutFiles())
        {
            bool sameName = Path.GetFileName(lnk).Equals(linkName, StringComparison.OrdinalIgnoreCase);
            var target = Launcher.ResolveTarget(lnk);
            if (target == null || !File.Exists(target)) continue;
            bool sameExe = Path.GetFileName(target).Equals(exeName, StringComparison.OrdinalIgnoreCase);
            if (sameExe) candidates.Add((target, sameName));
        }
        // Même nom de raccourci d'abord, puis la cible la plus récente.
        return candidates
            .OrderByDescending(c => c.sameName)
            .ThenByDescending(c => File.GetLastWriteTimeUtc(c.target))
            .Select(c => c.target)
            .FirstOrDefault();
    }

    private static IEnumerable<string> OtherShortcutFiles()
    {
        var roots = new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Programs), true),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), true),
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), false),
        };
        foreach (var (root, recursive) in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.lnk", new EnumerationOptions
                {
                    RecurseSubdirectories = recursive,
                    IgnoreInaccessible = true,
                    MaxRecursionDepth = 4,
                }).ToList();
            }
            catch { continue; }
            foreach (var f in files) yield return f;
        }
    }

    /// <summary>
    /// Même exécutable dans un dossier voisin : …\Versions\version-ancien\app.exe → …\Versions\version-nouveau\app.exe.
    /// On remonte d'un ou deux niveaux pour couvrir …\app-1.2.3\bin\app.exe.
    /// </summary>
    private static string? FromSiblingVersion(string oldTarget)
    {
        var changed = Path.GetDirectoryName(oldTarget);
        for (int level = 0; level < 2 && changed != null; level++)
        {
            var parent = Path.GetDirectoryName(changed);
            if (parent == null || Path.GetPathRoot(parent) == parent) return null;
            if (Directory.Exists(parent))
            {
                string rel = Path.GetRelativePath(changed, oldTarget);
                try
                {
                    var found = Directory.EnumerateDirectories(parent)
                        .Select(d => Path.Combine(d, rel))
                        .Where(File.Exists)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                    if (found != null) return found;
                }
                catch { /* dossier inaccessible */ }
                return null; // le parent existe : inutile de remonter plus haut
            }
            changed = parent;
        }
        return null;
    }

    // ---------- Utilitaires ----------

    private static void Backup(ShortcutItem item)
    {
        try
        {
            var dir = Path.Combine(ConfigStore.DataDir, "Sauvegardes", "Raccourcis réparés");
            Directory.CreateDirectory(dir);
            var name = $"{Path.GetFileNameWithoutExtension(item.Path)} {DateTime.Now:yyyyMMdd-HHmmss}.lnk";
            File.Copy(item.Path, Path.Combine(dir, name), overwrite: false);
        }
        catch (Exception ex) { Log.Error("Sauvegarde du raccourci avant réparation", ex); }
    }

    private static string Read(Action<StringBuilder, int> getter)
    {
        var sb = new StringBuilder(1024);
        getter(sb, sb.Capacity);
        return sb.ToString();
    }

    private static string Read(GetPathFn getter)
    {
        var sb = new StringBuilder(1024);
        getter(sb, sb.Capacity, IntPtr.Zero, 0);
        return sb.ToString();
    }

    private delegate void GetPathFn(StringBuilder file, int cch, IntPtr findData, uint flags);

    private static bool SamePath(string a, string b)
        => string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
}
