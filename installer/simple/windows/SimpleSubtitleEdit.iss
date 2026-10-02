; Simple Subtitle Edit - Windows installer (Inno Setup 6.3+)
; Build: ISCC.exe /DAppVersion=0.1.0 /DArch=x64 /DSourceDir=..\..\..\publish\windows-x64 SimpleSubtitleEdit.iss
; The app is self-contained (.NET inside) and carries mpv, ffmpeg and mkvmerge.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\..\publish\windows-" + Arch
#endif

#define AppName "Simple Subtitle Edit"
#define AppExe "SubtitleEdit.exe"

[Setup]
AppId={{6B0E3C2A-7D41-4F7B-9E1A-5C3D2B8F9A10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Simple Subtitle Edit (fork of Subtitle Edit by Nikolaj Olsson)
AppPublisherURL=https://github.com/kevinkirsten/simple-subtitle-edit
AppSupportURL=https://github.com/kevinkirsten/simple-subtitle-edit/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\..\..\dist
OutputBaseFilename=Simple-Subtitle-Edit-{#AppVersion}-Windows-{#Arch}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
LicenseFile=..\..\..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
