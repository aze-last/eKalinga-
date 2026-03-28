#define MyAppName "Barangay Ayuda System"
#define MyAppPublisher "Barangay Ayuda System"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
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
DefaultDirName={autopf64}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile={#MyProjectDir}\Images\municipal-house.ico
OutputDir={#MyOutputDir}
OutputBaseFilename=BarangayAyudaSystem-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
ChangesAssociations=no
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
