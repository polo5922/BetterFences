using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BetterFences.Models;

namespace BetterFences.Services;

/// <summary>
/// Import best-effort depuis les instantanés XML de Fences (%AppData%\Stardock\Fences\Backups\*.xml).
/// Lecture seule côté Fences : rien n'y est modifié.
///
/// Format observé (Fences 5/6) :
///   Groups/ArrayOfString : [index, "id|x|y|w|h|?|Titre`avec`espaces` |\\.\DISPLAYn` |monX|monY|monW|monH|…|{GUID} |groupeOnglets|indexOnglet|…"]
///   Icons/ArrayOfString  : [chemin, "idFence|x|y|xRel|yRel|…"]  (idFence = -1 : hors fence)
/// Les fences partageant le même « groupeOnglets » (≠ 0) sont les onglets d'un même panneau.
/// </summary>
public static class FencesImporter
{
    private record Fence(int Id, string Title, double X, double Y, double W, double H, int TabGroup, int TabIndex);

    public static string? FindLatestSnapshot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dirs = new[]
        {
            Path.Combine(appData, "Stardock", "Fences", "Backups"),
            Path.Combine(appData, "Stardock", "Fences"),
            Path.Combine(appData, "Stardock", "Fences6"),
        };

        string? best = null;
        DateTime bestTime = DateTime.MinValue;
        foreach (var dir in dirs.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.xml"))
            {
                try
                {
                    var root = XDocument.Load(file).Root;
                    if (root?.Name.LocalName != "SnapshotInfo" || root.Element("Groups") == null) continue;
                    var time = DateTime.TryParse((string?)root.Element("Timestamp"), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var t) ? t : File.GetLastWriteTime(file);
                    if (time > bestTime) { bestTime = time; best = file; }
                }
                catch { /* fichier illisible ignoré */ }
            }
        }
        return best;
    }

    public static List<FolderModel> Import(string snapshotPath, bool moveShortcuts, out int itemCount)
    {
        itemCount = 0;
        var root = XDocument.Load(snapshotPath).Root!;
        var fences = new List<Fence>();

        foreach (var arr in root.Element("Groups")?.Elements("ArrayOfString") ?? Enumerable.Empty<XElement>())
        {
            var strings = arr.Elements("string").Select(s => s.Value).ToList();
            if (strings.Count < 2) continue;
            var fence = ParseFence(strings[1]);
            if (fence != null) fences.Add(fence);
        }

        // Raccourcis par fence, triés selon leur position dans la fence.
        var itemsByFence = new Dictionary<int, List<(string Path, double X, double Y)>>();
        foreach (var arr in root.Element("Icons")?.Elements("ArrayOfString") ?? Enumerable.Empty<XElement>())
        {
            var strings = arr.Elements("string").Select(s => s.Value).ToList();
            if (strings.Count < 2 || strings[0].StartsWith("::")) continue;
            var f = strings[1].Split('|');
            if (f.Length < 5 || !int.TryParse(f[0], out int fenceId) || fenceId < 0) continue;
            if (!itemsByFence.TryGetValue(fenceId, out var list)) itemsByFence[fenceId] = list = new();
            list.Add((strings[0], Num(f[3]), Num(f[4])));
        }

        var folders = new List<FolderModel>();
        var groups = fences
            .GroupBy(f => f.TabGroup == 0 ? -f.Id - 1 : f.TabGroup) // fences sans onglets : un groupe chacune
            .Select(g => g.OrderBy(f => f.TabIndex).ToList());

        foreach (var group in groups)
        {
            var first = group[0];
            var folder = new FolderModel
            {
                Name = first.Title,
                Mode = FolderMode.Panel,
                X = first.X,
                Y = first.Y,
                Width = Math.Max(260, first.W + 16),
                Height = Math.Max(160, first.H + 16),
            };

            foreach (var fence in group)
            {
                var tab = new TabModel(fence.Title);
                if (itemsByFence.TryGetValue(fence.Id, out var items))
                {
                    foreach (var (path, _, _) in items.OrderBy(i => Math.Round(i.Y / 20)).ThenBy(i => i.X))
                    {
                        var item = ShortcutStorage.Import(path, moveShortcuts);
                        if (item == null) continue;
                        tab.SubTabs[0].Items.Add(item);
                        itemCount++;
                    }
                }
                folder.Tabs.Add(tab);
            }
            folder.EnsureValid();
            folders.Add(folder);
        }
        return folders;
    }

    private static Fence? ParseFence(string data)
    {
        var f = data.Split('|');
        if (f.Length < 12 || !int.TryParse(f[0], out int id)) return null;

        string title = f[6].Replace('`', ' ').Trim();
        if (title.Length == 0) title = "Fence";

        // Les coordonnées sont relatives à un espace décalé ; on se ramène au moniteur indiqué.
        double x = Num(f[1]) - Num(f[8]);
        double y = Num(f[2]) - Num(f[9]);
        string device = f[7].Replace('`', ' ').Trim();
        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, device, StringComparison.OrdinalIgnoreCase));
        if (screen != null) { x += screen.Bounds.Left; y += screen.Bounds.Top; }

        int guidIdx = Array.FindIndex(f, s => s.TrimStart().StartsWith("{"));
        int tabGroup = 0, tabIndex = 0;
        if (guidIdx >= 0 && guidIdx + 2 < f.Length)
        {
            int.TryParse(f[guidIdx + 1], out tabGroup);
            int.TryParse(f[guidIdx + 2], out tabIndex);
        }
        return new Fence(id, title, x, y, Num(f[3]), Num(f[4]), tabGroup, tabIndex);
    }

    private static double Num(string s)
        => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}
