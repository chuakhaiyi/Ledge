#ifndef AppVersion
  #define AppVersion "1.0.4"
#endif
#ifndef PublishDir
  #define PublishDir SourcePath + "..\artifacts\app"
#endif

[Setup]
AppId={{AE2E5E6F-8C43-4E85-9ADB-2EE21AA81C3F}
AppName=Ledge
AppVersion={#AppVersion}
AppPublisher=Ledge
DefaultDirName={localappdata}\Ledge
DefaultGroupName=Ledge
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts\installer
OutputBaseFilename=Ledge-Setup
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\Ledge.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
AppMutex=Local\Ledge
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
Uninstallable=yes

[Tasks]
Name: "autostart"; Description: "Start Ledge when I sign in to Windows"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\Ledge.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Ledge"; Filename: "{app}\Ledge.exe"; IconFilename: "{app}\Ledge.exe"
Name: "{autodesktop}\Ledge"; Filename: "{app}\Ledge.exe"; IconFilename: "{app}\Ledge.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Ledge.exe"; Parameters: "--configure-startup={code:StartupChoice}"; Flags: runhidden waituntilterminated
Filename: "{app}\Ledge.exe"; Description: "Open Ledge"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Ledge"; Flags: uninsdeletevalue

[Code]
function StartupChoice(Param: String): String;
begin
  if WizardIsTaskSelected('autostart') then Result := 'true'
  else Result := 'false';
end;

// Notes and settings live in AppData\Roaming\Ledge and are deliberately
// absent from UninstallDelete: uninstalling the app never deletes notes.
