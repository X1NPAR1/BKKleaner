; BKKleaner - professional Inno Setup installer
; Build with:  iscc Installer\BKKleaner.iss
; Expects the self-contained publish output in publish\win-x64\

#define MyAppName "BKKleaner"
#define MyAppVersion "3.7.0"
#define MyAppPublisher "X1NPAR1"
#define MyAppURL "https://github.com/X1NPAR1/BKKleaner"
#define MyAppExeName "BKKleaner.exe"

[Setup]
AppId={{8E1F4C0A-9B7D-4B68-A1F2-6C3D5E9A0B17}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=EULA.txt
OutputDir=..\artifacts
OutputBaseFilename=BKKleaner-Setup-{#MyAppVersion}
SetupIconFile=..\logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousLanguage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "launchstartup"; Description: "{cm:AutoStartProgram,{#MyAppName}}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\logo.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"; Tasks: desktopicon

[Registry]
; Persist the wizard language so the app starts in the chosen language on first run.
Root: HKLM; Subkey: "SOFTWARE\BKKleaner"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\BKKleaner"; ValueType: string; ValueName: "DefaultLanguage"; ValueData: "{code:GetLangCode}"
Root: HKLM; Subkey: "SOFTWARE\BKKleaner"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
Root: HKLM; Subkey: "SOFTWARE\BKKleaner"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"
; Optional auto-start at login.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BKKleaner"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: launchstartup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\BKKleaner\Logs"

[Code]
{ Maps the chosen Inno wizard language to the app's two-letter language code. }
function GetLangCode(Param: String): String;
var
  Lang: String;
begin
  Lang := ActiveLanguage();
  if Lang = 'turkish' then Result := 'tr'
  else if Lang = 'german' then Result := 'de'
  else if Lang = 'dutch' then Result := 'nl'
  else if Lang = 'russian' then Result := 'ru'
  else Result := 'en';
end;
