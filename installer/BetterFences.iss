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
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "startup"; Description: "Lancer BetterFences au démarrage de Windows"; GroupDescription: "Options :"
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Options :"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Même valeur que l'option « Lancer avec Windows » du menu de l'appli.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Lancer BetterFences maintenant"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Les données (%AppData%\BetterFences : configuration et raccourcis rangés) sont conservées volontairement.
Type: filesandordirs; Name: "{app}"

[Code]
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
begin
  StopRunningApp();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Retire aussi l'entrée de démarrage si elle a été ajoutée depuis le menu de l'appli.
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
    // Entrées du menu contextuel de l'Explorateur ajoutées par l'appli.
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\DesktopBackground\Shell\BetterFences.NewFolder');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\BetterFences.Import');
  end;
end;
