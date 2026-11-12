using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using BetterFences.Models;

namespace BetterFences.Services;

public static class ConfigStore
{
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("BETTERFENCES_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterFences");

    public static string ConfigPath => Path.Combine(DataDir, "config.json");
    private static string BackupPath => Path.Combine(DataDir, "config.bak.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static DispatcherTimer? _timer;

    public static bool ConfigExists => File.Exists(ConfigPath);

    public static AppConfig Load()
    {
        Directory.CreateDirectory(DataDir);
        foreach (var path in new[] { ConfigPath, BackupPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options);
                if (cfg != null) return cfg;
            }
            catch (Exception ex)
            {
                Log.Error($"Lecture de {path} impossible", ex);
            }
        }
        return new AppConfig();
    }

    public static string Serialize(AppConfig cfg) => JsonSerializer.Serialize(cfg, Options);
    public static AppConfig? Deserialize(string json) => JsonSerializer.Deserialize<AppConfig>(json, Options);

    /// <summary>Sauvegarde différée (regroupe les modifications rapprochées).</summary>
    public static void RequestSave()
    {
        if (_timer == null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (_, _) => { _timer.Stop(); SaveNow(); };
        }
        _timer.Stop();
        _timer.Start();
    }

    public static void SaveNow()
    {
        _timer?.Stop();
        try
        {
            Directory.CreateDirectory(DataDir);
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, Serialize(App.Config));
            if (File.Exists(ConfigPath))
                File.Replace(tmp, ConfigPath, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(tmp, ConfigPath);
        }
        catch (Exception ex)
        {
            Log.Error("Sauvegarde de la configuration impossible", ex);
        }
    }
}

public static class Log
{
    public static void Error(string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.DataDir);
            File.AppendAllText(Path.Combine(ConfigStore.DataDir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{(ex != null ? Environment.NewLine + ex : "")}{Environment.NewLine}");
        }
        catch { /* rien à faire */ }
    }
}
