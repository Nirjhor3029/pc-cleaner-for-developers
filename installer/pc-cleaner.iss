; PC Cleaner Inno Setup Script
; Run with ISCC.exe from Inno Setup 6

#define MyAppName "PC Cleaner"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PcCleaner"
#define MyAppExeName "PcCleaner.App.exe"

[Setup]
AppId={{8F2C3B7A-5D4E-4C21-9D1A-2A3B4C5D6E7F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PC Cleaner
DefaultGroupName=PC Cleaner
OutputDir=.
OutputBaseFilename=PC-Cleaner-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\src\PcCleaner.App\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PC Cleaner"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall PC Cleaner"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PC Cleaner"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
