using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace BetterFences.Services;

/// <summary>
/// Lancement avec Windows via une tâche planifiée « à l'ouverture de session ».
/// Contrairement à la clé Run (que Windows retarde de plusieurs secondes après l'ouverture de session),
/// la tâche démarre tout de suite, en priorité normale, sans limite de durée ni restriction sur batterie.
/// Elle tourne avec les droits normaux de l'utilisateur : une appli élevée ne pourrait plus recevoir
/// les glisser-déposer de l'Explorateur. Si la tâche ne peut pas être créée, on se replie sur la clé Run.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "BetterFences";
    public const string SetStartupArg = "--set-startup";

    public static bool IsEnabled => TaskExists() || RunValueExists();

    /// <summary>Active ou désactive le lancement avec Windows. Renvoie false en cas d'échec.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                if (TaskExists()) Schtasks($"/Delete /TN \"{Name}\" /F");
                DeleteRunValue();
                return !IsEnabled;
            }
            if (CreateTask())
            {
                DeleteRunValue(); // l'ancienne méthode, plus lente, n'est plus utile
                return true;
            }
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(Name, $"\"{Environment.ProcessPath}\"");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Modification du démarrage automatique impossible", ex);
            return false;
        }
    }

    /// <summary>« --set-startup on|off » (utilisé par l'installateur) : renvoie le code de sortie, ou null.</summary>
    public static int? HandleArgs(string[] args)
    {
        if (args.Length == 0 || args[0] != SetStartupArg) return null;
        bool on = args.Length < 2 || !args[1].Equals("off", StringComparison.OrdinalIgnoreCase);
        return SetEnabled(on) ? 0 : 1;
    }

    /// <summary>Passe une ancienne installation (clé Run) à la tâche planifiée, plus rapide.</summary>
    public static void MigrateLegacy()
    {
        try
        {
            if (RunValueExists() && CreateTask()) DeleteRunValue();
        }
        catch (Exception ex)
        {
            Log.Error("Migration du démarrage automatique", ex);
        }
    }

    private static bool CreateTask()
    {
        var user = WindowsIdentity.GetCurrent().Name;
        var exe = Environment.ProcessPath!;
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Affiche les dossiers BetterFences sur le bureau dès l'ouverture de session.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>LeastPrivilege</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>"{SecurityElement.Escape(exe)}"</Command>
                  <WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exe))}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
        var tmp = Path.Combine(Path.GetTempPath(), $"BetterFences-task-{Environment.ProcessId}.xml");
        try
        {
            File.WriteAllText(tmp, xml, Encoding.Unicode);
            return Schtasks($"/Create /TN \"{Name}\" /XML \"{tmp}\" /F") && TaskExists();
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* fichier temporaire */ }
        }
    }

    private static bool TaskExists() => Schtasks($"/Query /TN \"{Name}\"");

    private static bool Schtasks(string arguments)
    {
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        if (p == null) return false;
        p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit(15_000);
        if (p.ExitCode != 0 && !arguments.StartsWith("/Query")) Log.Error("schtasks " + arguments, new Exception(err.Trim()));
        return p.ExitCode == 0;
    }

    private static bool RunValueExists()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(Name) is string;
    }

    private static void DeleteRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(Name, throwOnMissingValue: false);
    }
}
