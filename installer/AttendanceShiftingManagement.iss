#define MyAppName "eKalinga+"
#define MyAppPublisher "eKalinga+ Solutions"
#define MyAppURL "https://github.com/BarangayAyudaSys"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.5.0"
#endif

#ifndef MyAppExeName
  #define MyAppExeName "AttendanceShiftingManagement.exe"
#endif

#ifndef MyPublishDir
  #error "MyPublishDir must be provided by the build script."
#endif

#ifndef MyOutputDir
  #error "MyOutputDir must be provided by the build script."
#endif

#ifndef MyProjectDir
  #error "MyProjectDir must be provided by the build script."
#endif

[Setup]
AppId={{D788D4E9-E5AF-4F5B-8758-EFD3D8B15E33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile={#MyProjectDir}\Images\municipal-house.ico
OutputDir={#MyOutputDir}
OutputBaseFilename=eKalingaPlus-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
ChangesAssociations=no
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#MyAppExeName}"
Source: "{#MyProjectDir}\Images\municipal-house.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\municipal-house.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\municipal-house.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  // Check for VC++ 2015-2022 Redistributable (x64)
  // This is a common cause for AForge/Native DLL crashes
  if not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64') then
  begin
    if MsgBox('eKalinga+ requires the Visual C++ 2015-2022 Redistributable to run the Camera/OCR modules. Would you like to download it now?', mbConfirmation, MB_YESNO) = idYes then
    begin
      ShellExec('open', 'https://aka.ms/vs/17/release/vc_redist.x64.exe', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      MsgBox('Please install the Redistributable and then run this installer again.', mbInformation, MB_OK);
      Result := False;
    end;
  end;
end;
