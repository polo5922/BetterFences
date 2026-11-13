; Installateur BetterFences (Inno Setup 6) — généré par tools\build-installer.ps1
; Installation par utilisateur (pas besoin des droits administrateur).

#define AppName "BetterFences"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExe "BetterFences.exe"

[Setup]
AppId={{6B1F3C2A-8E4D-4F7B-9A51-BF2E7C0D9A13}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=BetterFences-Setup-{#AppVersion}
SetupIconFile=..\src\BetterFences\Assets\BetterFences.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
; Style sombre assorti à l'appli : fond dégradé bleu nuit, images générées par tools\make-installer-art.py.
WizardStyle=modern dark windows11 hidebevels
WizardBackColor=#15171D
WizardBackImageFile=art\background.png
WizardBackImageOpacity=255
WizardImageFile=art\wizard-100.png,art\wizard-125.png,art\wizard-150.png,art\wizard-200.png,art\wizard-250.png
WizardSmallImageFile=art\small-100.png,art\small-125.png,art\small-150.png,art\small-200.png,art\small-250.png
WizardImageStretch=yes
DisableWelcomePage=no
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "startup"; Description: "Lancer BetterFences au démarrage de Windows (démarrage rapide)"; GroupDescription: "Options :"
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Options :"; Flags: unchecked
Name: "shellmenu"; Description: "Ajouter BetterFences au clic droit du bureau et des dossiers (demande les droits administrateur)"; GroupDescription: "Options :"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "--show"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "--show"; Tasks: desktopicon

[Run]
; Démarrage rapide : tâche planifiée « à l'ouverture de session » (remplace l'ancienne clé Run, que Windows retarde).
Filename: "{app}\{#AppExe}"; Parameters: "--set-startup on"; Tasks: startup; Flags: waituntilterminated runhidden; StatusMsg: "Configuration du démarrage rapide..."
Filename: "{app}\{#AppExe}"; Parameters: "--set-startup off"; Tasks: not startup; Flags: waituntilterminated runhidden
; Entrées du menu contextuel (HKLM) : l'appli se relance avec les droits administrateur.
Filename: "{app}\{#AppExe}"; Parameters: "--register-shell-menu"; Verb: "runas"; Tasks: shellmenu; Check: ShellMenuMissing;   Flags: shellexec waituntilterminated runhidden; StatusMsg: "Ajout au menu contextuel..."
Filename: "{app}\{#AppExe}"; Parameters: "--show"; Description: "Lancer BetterFences maintenant"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Les données (%AppData%\BetterFences : configuration et raccourcis rangés) sont conservées volontairement.
Type: filesandordirs; Name: "{app}"

[Code]
// Évite une nouvelle demande d'autorisation à chaque mise à jour si les entrées existent déjà.
function ShellMenuMissing(): Boolean;
begin
  Result := not RegKeyExists(HKLM, 'Software\Classes\DesktopBackground\Shell\BetterFences.NewFolder');
end;

procedure StopRunningApp();
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
var
  Code: Integer;
begin
  StopRunningApp();
  // Tâche planifiée de démarrage.
  Exec(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "{#AppName}" /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  // Entrées du menu contextuel écrites dans HKLM : retrait avec les droits administrateur.
  if RegKeyExists(HKLM, 'Software\Classes\DesktopBackground\Shell\BetterFences.NewFolder') or
     RegKeyExists(HKLM, 'Software\Classes\Directory\shell\BetterFences.Import') then
    ShellExec('runas', ExpandConstant('{app}\{#AppExe}'), '--unregister-shell-menu', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Ancienne entrée de démarrage (clé Run, versions 1.0 à 1.1).
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
    // Anciennes entrées du menu contextuel (version 1.1.0, HKCU).
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\DesktopBackground\Shell\BetterFences.NewFolder');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\BetterFences.Import');
  end;
end;
