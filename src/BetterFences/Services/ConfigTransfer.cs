using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BetterFences.Models;

namespace BetterFences.Services;

/// <summary>
/// Export / import de la configuration dans une archive .zip : config.json + les raccourcis rangés
/// dans Items\ (ceux qui ont été retirés du bureau). Un config.json seul peut aussi être importé.
/// Rien n'est jamais supprimé : un fichier déjà présent n'est pas écrasé.
/// </summary>
public static class ConfigTransfer
{
    private const string ConfigEntry = "config.json";
    public static string BackupDir => Path.Combine(ConfigStore.DataDir, "Sauvegardes");

    public static void Export()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exporter la configuration BetterFences",
            Filter = "Configuration BetterFences (*.zip)|*.zip",
            FileName = $"BetterFences-{DateTime.Now:yyyy-MM-dd}.zip",
            DefaultExt = ".zip",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            int files = WriteArchive(dlg.FileName);
            Dialogs.Info($"Configuration exportée : {App.Config.Folders.Count} dossier(s), {files} raccourci(s) rangé(s).\n\n{dlg.FileName}");
        }
        catch (Exception ex)
        {
            Log.Error("Export de la configuration", ex);
            Dialogs.Info("L'export a échoué : " + ex.Message);
        }
    }

    /// <summary>Écrit l'archive et renvoie le nombre de raccourcis inclus.</summary>
    public static int WriteArchive(string file)
    {
        ConfigStore.SaveNow();
        var tmp = file + ".tmp";
        int files = 0;
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(ConfigEntry);
            using (var w = new StreamWriter(entry.Open()))
                w.Write(ConfigStore.Serialize(App.Config));

            foreach (var item in App.Config.Folders.SelectMany(f => f.AllItems))
            {
                if (!item.Owned || !File.Exists(item.Path)) continue;
                zip.CreateEntryFromFile(item.Path, $"Items/{item.Id}/{Path.GetFileName(item.Path)}");
                files++;
            }
        }
        File.Move(tmp, file, overwrite: true);
        return files;
    }

    public static void Import()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importer une configuration BetterFences",
            Filter = "Configuration BetterFences (*.zip, config.json)|*.zip;*.json",
        };
        if (dlg.ShowDialog() != true) return;
        Import(dlg.FileName, null);
    }

    /// <summary>Importe un fichier ; <paramref name="replace"/> null = demander à l'utilisateur.</summary>
    public static void Import(string path, bool? replace)
    {
        ZipArchive? zip = null;
        try
        {
            AppConfig? cfg;
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                cfg = ConfigStore.Deserialize(File.ReadAllText(path));
            }
            else
            {
                zip = ZipFile.OpenRead(path);
                var entry = zip.GetEntry(ConfigEntry) ?? throw new InvalidDataException("config.json absent de l'archive.");
                using var r = new StreamReader(entry.Open());
                cfg = ConfigStore.Deserialize(r.ReadToEnd());
            }
            if (cfg == null || cfg.Folders.Count == 0)
            {
                Dialogs.Info("Ce fichier ne contient aucun dossier BetterFences.");
                return;
            }

            replace ??= Dialogs.YesNoCancel(
                $"Importer {cfg.Folders.Count} dossier(s).\n\n" +
                "Oui : remplacer la configuration actuelle. Elle est d'abord sauvegardée dans le dossier des données " +
                "(Sauvegardes), et ses raccourcis rangés y restent : rien n'est supprimé.\n" +
                "Non : ajouter ces dossiers à ceux déjà présents.");
            if (replace == null) return;

            if (replace == true)
            {
                Directory.CreateDirectory(BackupDir);
                WriteArchive(Path.Combine(BackupDir, $"avant-import-{DateTime.Now:yyyy-MM-dd-HHmmss}.zip"));
            }

            var usedFolderIds = replace == true ? new HashSet<string>() : App.Config.Folders.Select(f => f.Id).ToHashSet();
            var usedItemIds = replace == true
                ? new HashSet<string>()
                : App.Config.Folders.SelectMany(f => f.AllItems).Select(i => i.Id).ToHashSet();

            foreach (var f in cfg.Folders)
            {
                if (!usedFolderIds.Add(f.Id)) f.Id = NewId();
                foreach (var item in f.AllItems)
                {
                    var oldId = item.Id;
                    if (!IsSafeId(item.Id) || !usedItemIds.Add(item.Id))
                    {
                        item.Id = NewId();
                        usedItemIds.Add(item.Id);
                    }
                    if (item.Owned) RelocateOwned(item, oldId, zip);
                }
                f.EnsureValid();
            }

            if (replace == true) App.Instance.ReplaceConfig(cfg);
            else App.Instance.AddFolders(cfg.Folders);
            ConfigStore.SaveNow();
            Dialogs.Info($"{cfg.Folders.Count} dossier(s) importé(s).");
        }
        catch (Exception ex)
        {
            Log.Error("Import de la configuration", ex);
            Dialogs.Info("L'import a échoué : " + ex.Message);
        }
        finally
        {
            zip?.Dispose();
        }
    }

    /// <summary>Range le raccourci dans Items\{id}\ : extrait de l'archive, ou copié depuis son ancien emplacement.</summary>
    private static void RelocateOwned(ShortcutItem item, string oldId, ZipArchive? zip)
    {
        var name = Path.GetFileName(item.Path);
        if (string.IsNullOrEmpty(name)) { item.Owned = false; return; }
        var dest = Path.Combine(ShortcutStorage.ItemsDir, item.Id, name);

        if (!File.Exists(dest))
        {
            var entry = zip?.GetEntry($"Items/{oldId}/{name}");
            if (entry != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: false);
            }
            else if (File.Exists(item.Path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(item.Path, dest, overwrite: false);
            }
        }

        if (File.Exists(dest)) item.Path = dest;
        else item.Owned = false; // introuvable : simple référence, on ne prétend pas pouvoir le restaurer
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
    private static bool IsSafeId(string id) => Guid.TryParseExact(id, "N", out _);
}
