#define MyAppName "F1Telemetry"
#define MyAppVersion "1.0.1-beta1"
#define MyAppExeName "F1Telemetry.App.exe"

[Setup]
AppName={#MyAppName}
AppId={{F1Telemetry}}
AppVersion={#MyAppVersion}
AppPublisher=F1Telemetry
DefaultDirName={autopf}\F1Telemetry
DefaultGroupName=F1Telemetry
DisableDirPage=yes
OutputDir=output
OutputBaseFilename=F1Telemetry-Setup-1.0.1-beta1
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
CloseApplications=yes
CloseApplicationsFilter=F1Telemetry.App.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\F1Telemetry"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\F1Telemetry"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch F1Telemetry"; Flags: nowait postinstall skipifsilent
