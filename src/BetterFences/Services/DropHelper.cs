using System.Windows;
using BetterFences.Models;

namespace BetterFences.Services;

public static class DragState
{
    public const string Format = "BetterFences.Item";
    public static ShortcutItem? Item { get; set; }
    public static bool Active { get; set; }
}

public static class DropHelper
{
    public static void DragOver(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DragState.Format) && DragState.Item != null)
            e.Effects = DragDropEffects.Move;
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Dépose dans <paramref name="target"/> ; index = position d'insertion (-1 = fin).</summary>
    public static void Drop(FolderModel folder, SubTabModel? target, int index, DragEventArgs e)
    {
        e.Handled = true;
        if (target == null) return;

        if (e.Data.GetDataPresent(DragState.Format) && DragState.Item is { } item)
        {
            FolderActions.MoveItem(item, folder, target, index);
            e.Effects = DragDropEffects.Move;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var path in files)
            {
                var newItem = ShortcutStorage.Import(path, move: true);
                if (newItem == null) continue;
                if (index >= 0 && index <= target.Items.Count) target.Items.Insert(index++, newItem);
                else target.Items.Add(newItem);
            }
            folder.NotifyChanged();
            // « Copie » pour que l'Explorateur ne tente pas lui-même de supprimer la source.
            e.Effects = DragDropEffects.Copy;
        }
    }
}
