using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;
using BetterFences.Services;
using MColor = System.Windows.Media.Color;

namespace BetterFences.Models;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public enum FolderMode { Icon, Panel }

/// <summary>Un raccourci (fichier .lnk/.url déplacé dans le stockage, ou simple référence vers un fichier).</summary>
public class ShortcutItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    /// <summary>Chemin actuel du fichier lancé (dans Items\ si Owned, sinon chemin d'origine).</summary>
    public string Path { get; set; } = "";

    /// <summary>Emplacement d'origine (pour pouvoir le remettre sur le bureau).</summary>
    public string? OriginalPath { get; set; }

    /// <summary>true si le fichier a été déplacé dans le stockage BetterFences.</summary>
    public bool Owned { get; set; }

    private ImageSource? _icon;
    private bool _iconRequested;

    [JsonIgnore]
    public ImageSource? Icon
    {
        get
        {
            if (_icon == null && !_iconRequested)
            {
                _iconRequested = true;
                IconService.Queue(this);
            }
            return _icon;
        }
    }

    internal void SetIcon(ImageSource? icon)
    {
        _icon = icon;
        OnPropertyChanged(nameof(Icon));
    }
}

public class SubTabModel : ObservableObject
{
    public SubTabModel() { }
    public SubTabModel(string name) { _name = name; }

    private string _name = "Tout";
    public string Name { get => _name; set => Set(ref _name, value); }

    public ObservableCollection<ShortcutItem> Items { get; set; } = new();
}

public class TabModel : ObservableObject
{
    public TabModel() { }
    public TabModel(string name)
    {
        _name = name;
        SubTabs.Add(new SubTabModel("Tout"));
    }

    private string _name = "Général";
    public string Name { get => _name; set => Set(ref _name, value); }

    public ObservableCollection<SubTabModel> SubTabs { get; set; } = new();

    private int _selectedSubTabIndex;
    public int SelectedSubTabIndex
    {
        get => _selectedSubTabIndex;
        set { if (Set(ref _selectedSubTabIndex, value)) OnPropertyChanged(nameof(SelectedSubTab)); }
    }

    [JsonIgnore]
    public SubTabModel? SelectedSubTab
    {
        get => _selectedSubTabIndex >= 0 && _selectedSubTabIndex < SubTabs.Count
            ? SubTabs[_selectedSubTabIndex]
            : SubTabs.FirstOrDefault();
        set
        {
            if (value == null) return;
            var i = SubTabs.IndexOf(value);
            if (i >= 0) SelectedSubTabIndex = i;
        }
    }

    public void EnsureValid()
    {
        if (SubTabs.Count == 0) SubTabs.Add(new SubTabModel("Tout"));
        if (_selectedSubTabIndex < 0 || _selectedSubTabIndex >= SubTabs.Count) _selectedSubTabIndex = 0;
    }

    public void RefreshSelection() => OnPropertyChanged(nameof(SelectedSubTab));
}

public class FolderModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = "Dossier";
    public string Name { get => _name; set => Set(ref _name, value); }

    private FolderMode _mode = FolderMode.Icon;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FolderMode Mode { get => _mode; set => Set(ref _mode, value); }

    public double X { get; set; } = 200;
    public double Y { get; set; } = 200;
    public double Width { get; set; } = 460;
    public double Height { get; set; } = 340;

    private bool _collapsed;
    public bool Collapsed { get => _collapsed; set => Set(ref _collapsed, value); }

    private string _accentColor = "#4F7CFF";
    public string AccentColor
    {
        get => _accentColor;
        set { if (Set(ref _accentColor, value)) RefreshBrushes(); }
    }

    private double _opacity = 0.95;
    public double Opacity
    {
        get => _opacity;
        set { if (Set(ref _opacity, Math.Clamp(value, 0.3, 1.0))) RefreshBrushes(); }
    }

    public ObservableCollection<TabModel> Tabs { get; set; } = new();

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { if (Set(ref _selectedTabIndex, value)) OnPropertyChanged(nameof(SelectedTab)); }
    }

    [JsonIgnore]
    public TabModel? SelectedTab
    {
        get => _selectedTabIndex >= 0 && _selectedTabIndex < Tabs.Count ? Tabs[_selectedTabIndex] : Tabs.FirstOrDefault();
        set
        {
            if (value == null) return;
            var i = Tabs.IndexOf(value);
            if (i >= 0) SelectedTabIndex = i;
        }
    }

    [JsonIgnore] public IEnumerable<ShortcutItem> AllItems => Tabs.SelectMany(t => t.SubTabs).SelectMany(s => s.Items);
    [JsonIgnore] public List<ShortcutItem> PreviewItems => AllItems.Take(4).ToList();

    private Brush? _accentBrush, _backgroundBrush, _softAccentBrush;
    [JsonIgnore] public Brush AccentBrush { get { if (_accentBrush == null) BuildBrushes(); return _accentBrush!; } }
    [JsonIgnore] public Brush SoftAccentBrush { get { if (_softAccentBrush == null) BuildBrushes(); return _softAccentBrush!; } }
    [JsonIgnore] public Brush BackgroundBrush { get { if (_backgroundBrush == null) BuildBrushes(); return _backgroundBrush!; } }

    private void BuildBrushes()
    {
        MColor accent;
        try { accent = (MColor)ColorConverter.ConvertFromString(_accentColor); }
        catch { accent = MColor.FromRgb(0x4F, 0x7C, 0xFF); }

        static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
        byte alpha = (byte)Math.Round(_opacity * 255);
        var top = MColor.FromArgb(alpha, Mix(0x26, accent.R, 0.20), Mix(0x28, accent.G, 0.20), Mix(0x30, accent.B, 0.20));
        var bottom = MColor.FromArgb(alpha, Mix(0x16, accent.R, 0.10), Mix(0x17, accent.G, 0.10), Mix(0x1D, accent.B, 0.10));

        var bg = new LinearGradientBrush(top, bottom, 90);
        bg.Freeze();
        var ab = new SolidColorBrush(accent);
        ab.Freeze();
        var soft = new SolidColorBrush(MColor.FromArgb(0x55, accent.R, accent.G, accent.B));
        soft.Freeze();
        _backgroundBrush = bg;
        _accentBrush = ab;
        _softAccentBrush = soft;
    }

    private void RefreshBrushes()
    {
        _accentBrush = _backgroundBrush = _softAccentBrush = null;
        OnPropertyChanged(nameof(AccentBrush));
        OnPropertyChanged(nameof(SoftAccentBrush));
        OnPropertyChanged(nameof(BackgroundBrush));
    }

    public void EnsureValid()
    {
        if (Tabs.Count == 0) Tabs.Add(new TabModel("Général"));
        foreach (var t in Tabs) t.EnsureValid();
        if (_selectedTabIndex < 0 || _selectedTabIndex >= Tabs.Count) _selectedTabIndex = 0;
        if (Width < 220) Width = 220;
        if (Height < 140) Height = 140;
    }

    /// <summary>À appeler après toute modification du contenu : rafraîchit l'aperçu et sauvegarde.</summary>
    public void NotifyChanged()
    {
        OnPropertyChanged(nameof(PreviewItems));
        OnPropertyChanged(nameof(SelectedTab));
        foreach (var t in Tabs) t.RefreshSelection();
        ConfigStore.RequestSave();
    }
}

public class AppConfig
{
    public int Version { get; set; } = 1;
    public bool Locked { get; set; }
    /// <summary>Entrées BetterFences dans le menu contextuel de l'Explorateur.</summary>
    public bool ShellMenu { get; set; } = true;
    public List<FolderModel> Folders { get; set; } = new();
}
