; WsFiler InnoSetup Script
; Usage: ISCC.exe /DAppVersion=X.Y.Z /DSourceDir=<path> /DOutputDir=<path> WsFiler.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName=WsFiler
AppVersion={#AppVersion}
AppPublisher=yoshy3
AppPublisherURL=https://github.com/yoshy3/WsFiler
AppSupportURL=https://github.com/yoshy3/WsFiler/issues
DefaultDirName={autopf}\WsFiler
DefaultGroupName=WsFiler
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename=WsFiler-{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\WsFiler.App.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourceDir}\ja\*"; DestDir: "{app}\ja"; Flags: ignoreversion createallsubdirs recursesubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\WsFiler"; Filename: "{app}\WsFiler.App.exe"
Name: "{group}\{cm:UninstallProgram,WsFiler}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\WsFiler"; Filename: "{app}\WsFiler.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WsFiler.App.exe"; Description: "{cm:LaunchProgram,WsFiler}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
