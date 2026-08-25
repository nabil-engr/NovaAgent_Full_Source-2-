#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\publish\installer"
#endif

#define AppName "Nova Agent"
#define AppPublisher "Nova Agent"
#define AppExeName "NovaAgent.exe"
#define AppId "{{82C17E0B-518C-4EAB-A39A-D08A600CB58B}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\NovaAgent
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename=NovaAgent-Setup-{#AppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Uninstallable=yes
CreateUninstallRegKey=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\NovaAgent.Desktop
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "autostart"; Description: "Start Nova Agent with &Windows (minimized to tray)"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,PLACE_RUNTIME_HERE.txt"

[Icons]
Name: "{group}\Nova Agent"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall Nova Agent"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Nova Agent"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NovaAgent"; ValueData: """{app}\{#AppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Nova Agent"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\NovaAgent\Temp"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox('Nova Agent requires 64-bit Windows 10 or Windows 11.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    Log('Nova Agent installation completed successfully.');
end;
