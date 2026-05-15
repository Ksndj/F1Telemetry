#define MyAppName "F1Telemetry"
#define MyAppVersion "2.0.0-beta4"
#define MyAppExeName "F1Telemetry.App.exe"

[Setup]
AppName={#MyAppName}
AppId={{F1Telemetry}}
AppVersion={#MyAppVersion}
AppPublisher=F1Telemetry
DefaultDirName={autopf}\F1Telemetry
DefaultGroupName=F1Telemetry
DisableDirPage=no
ShowLanguageDialog=yes
OutputDir=output
OutputBaseFilename=F1Telemetry-2.0.0-beta4-win-x64-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=F1Telemetry.App.exe
SetupIconFile=..\F1Telemetry.App\Assets\AppIcon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,Languages\ChineseSimplified.isl"

[CustomMessages]
english.AdditionalShortcuts=Additional shortcuts:
english.DesktopIconTask=Create a desktop shortcut
english.LaunchAfterInstall=Launch F1Telemetry
chinesesimp.AdditionalShortcuts=附加快捷方式:
chinesesimp.DesktopIconTask=创建桌面快捷方式
chinesesimp.LaunchAfterInstall=启动 F1Telemetry

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIconTask}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "settings.json,*.db,*.sqlite,*.sqlite3,logs\*,.logs\*,*.log,*.jsonl,*.tmp,.env*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\F1Telemetry"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\F1Telemetry"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent
