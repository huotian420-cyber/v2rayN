#ifndef SourceDir
  #error SourceDir not defined
#endif

#ifndef OutputDir
  #error OutputDir not defined
#endif

#ifndef AppVersion
  #error AppVersion not defined
#endif

#ifndef IconFile
  #error IconFile not defined
#endif

#define AppName "v2rayN"
#define AppPublisher "huotian420-cyber"
#define AppEnvName "V2RAYN_LOCAL_APPLICATION_DATA_V2"
#define InstallerBaseName "v2rayN-setup-win-x64-" + AppVersion

[Setup]
AppId={{C15D7D44-20D1-42A6-9456-FDF2B9809309}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/huotian420-cyber/v2rayN
AppSupportURL=https://github.com/huotian420-cyber/v2rayN
AppUpdatesURL=https://github.com/huotian420-cyber/v2rayN
DefaultDirName={autopf}\v2rayN
DefaultGroupName=v2rayN
DisableProgramGroupPage=yes
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename={#InstallerBaseName}
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\v2rayN.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
ChangesEnvironment=yes
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "附加任务:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "AmazTool\*"
Source: "{#SourceDir}\AmazTool\AmazTool.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\v2rayN"; Filename: "{app}\v2rayN.exe"; WorkingDir: "{app}"; IconFilename: "{app}\v2rayN.exe"
Name: "{autodesktop}\v2rayN"; Filename: "{app}\v2rayN.exe"; WorkingDir: "{app}"; Tasks: desktopicon; IconFilename: "{app}\v2rayN.exe"

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "{#AppEnvName}"; ValueData: "1"; Flags: uninsdeletevalue
