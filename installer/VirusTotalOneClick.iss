#define AppName "VirusTotal OneClick"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishPath
  #define PublishPath "build\publish\win-x64"
#endif

[Setup]
AppId={{1F092688-8A02-4C5F-BDF8-47F550FEFE84}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
DefaultDirName={localappdata}\Programs\VirusTotalOneClick
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
OutputDir=output
OutputBaseFilename=VirusTotalOneClickSetup
UninstallDisplayIcon={app}\VirusTotalOneClick.exe

[Files]
Source: "..\{#PublishPath}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs ignoreversion

[InstallDelete]
Type: files; Name: "{app}\VirusTotalOneClick.pdb"

[Run]
Filename: "{app}\VirusTotalOneClick.exe"; Parameters: "install-shell"; Flags: runhidden waituntilterminated
Filename: "{app}\VirusTotalOneClick.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\VirusTotalOneClick.exe"; Parameters: "uninstall-shell"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallShell"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: string;
  Response: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if UninstallSilent then
      Exit;

    Response := MsgBox('Remove saved settings, upload history, and the stored API key?', mbConfirmation, MB_YESNO);
    if Response = IDYES then
    begin
      AppDataPath := ExpandConstant('{userappdata}\VirusTotalOneClick');
      DelTree(AppDataPath, True, True, True);
    end;
  end;
end;
