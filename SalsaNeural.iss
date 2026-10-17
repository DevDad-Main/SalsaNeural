; SalsaNeural — per-user installer (no administrator privileges).
; Installs to %LOCALAPPDATA%\Programs\SalsaNeural, or suggests I:\Apps\SalsaNOW\SalsaNeural
; when a GeForce NOW persistent-storage drive is present.

#define MyAppName "SalsaNeural"
#ifndef PublishDir
  #define PublishDir SourcePath + "dist\SalsaNeural"
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir SourcePath + "dist"
#endif
#ifndef MyAppVersion
  #define MyAppVersion GetVersionNumbersString(PublishDir + "\SalsaNeural.exe")
#endif
#define MyAppPublisher "DevDad"
#define MyAppURL "https://github.com/DevDad-Main/SalsaNeural"
#define MyAppExeName "SalsaNeural.exe"

[Setup]
AppId={{9F4B2C7E-1A5D-4E8F-B3C6-2D8A7E5F1C40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
; Per-user install — never requests administrator privileges.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=SalsaNeural-Setup
SetupIconFile={#PublishDir}\icon.ico
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SalsaNeural"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Suggest the SalsaNOW apps folder when a GeForce NOW persistent-storage drive exists.
function InitializeSetup(): Boolean;
var
  SalsaNowDir: String;
begin
  Result := True;
  SalsaNowDir := 'I:\Apps\SalsaNOW';
  if DirExists(SalsaNowDir) then
  begin
    if SuppressibleMsgBox('Detected SalsaNOW persistent storage (I:).' + #13#10 + #13#10 +
      'Install SalsaNeural to I:\Apps\SalsaNOW\SalsaNeural instead of AppData?' + #13#10 +
      '(Recommended — the I: drive persists across GeForce NOW sessions.)',
      mbConfirmation, MB_YESNO, IDYES) = IDYES
    then
      WizardForm.DirEdit.Text := 'I:\Apps\SalsaNOW\SalsaNeural';
  end;
end;
