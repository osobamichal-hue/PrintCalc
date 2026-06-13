#define MyAppName "3DPrintCalc"
#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif
#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "3DPrintCalc-Setup"
#endif
#define MyAppPublisher "3DPrintCalc"
#define MyAppExeName "PrintCalc.App.exe"
#define MyAppSourceDir "..\\artifacts\\publish\\win-x64"

[Setup]
AppId={{2D8A7307-F07A-4CC2-9870-C6CE841D96FE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename={#MyOutputBaseFilename}
PrivilegesRequired=admin

[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"

[Tasks]
Name: "desktopicon"; Description: "Vytvořit zástupce na ploše"; GroupDescription: "Další úlohy:"

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Excludes: "*.db,*.db-*,PrintCalc_Backup_*.zip,backup-manifest.json,appsettings-db.json,*.pdb,appsettings.json"
Source: "{#MyAppSourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Spustit {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure EnsureLegacyDbMigration();
var
  LegacyDbPath: string;
  TargetDbDir: string;
  TargetDbPath: string;
begin
  LegacyDbPath := ExpandConstant('{app}\printcalc.db');
  TargetDbDir := ExpandConstant('{localappdata}\PrintCalc');
  TargetDbPath := TargetDbDir + '\printcalc.db';

  if FileExists(TargetDbPath) then
    exit;
  if not FileExists(LegacyDbPath) then
    exit;

  ForceDirectories(TargetDbDir);
  if not CopyFile(LegacyDbPath, TargetDbPath, false) then
    MsgBox('Nepodařilo se převzít starší databázi z instalační složky. Spusťte obnovu ze zálohy.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    EnsureLegacyDbMigration();
end;
