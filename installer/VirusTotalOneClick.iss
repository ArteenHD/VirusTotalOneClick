#define AppName "VirusTotal OneClick"
#ifndef PublishPath
  #define PublishPath "build\publish\win-x64"
#endif

[Setup]
AppId={{1F092688-8A02-4C5F-BDF8-47F550FEFE84}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher=ArteenHD
AppSupportURL=https://github.com/ArteenHD/VirusTotalOneClick/issues
DefaultDirName={localappdata}\Programs\VirusTotalOneClick
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
Compression=lzma2
SolidCompression=yes
OutputDir=output
OutputBaseFilename=VirusTotalOneClickSetup
SetupIconFile=..\src\VirusTotalOneClick\Assets\app-icon.ico
UninstallDisplayIcon={app}\VirusTotalOneClick.exe

[Files]
Source: "..\{#PublishPath}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

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
    // Belt-and-braces: the app's own uninstall-shell verb (see [UninstallRun])
    // is the primary cleanup, but a corrupt or missing executable can't run
    // it. Remove the same shell surfaces directly, unconditionally, so a
    // broken install never leaves stale Explorer entries behind.
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Classes\*\shell\VirusTotalOneClick');
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Classes\Directory\shell\VirusTotalOneClick');
    DeleteFile(ExpandConstant('{userappdata}\Microsoft\Windows\SendTo\Upload to VirusTotal.lnk'));
    DeleteFile(ExpandConstant('{userappdata}\Microsoft\Windows\Start Menu\Programs\VirusTotal OneClick\VirusTotal OneClick.lnk'));
    RemoveDir(ExpandConstant('{userappdata}\Microsoft\Windows\Start Menu\Programs\VirusTotal OneClick'));
    DeleteFile(ExpandConstant('{userappdata}\Microsoft\Windows\Start Menu\Programs\Startup\VirusTotal OneClick Settings.lnk'));

    if UninstallSilent then
      Exit;

    Response := MsgBox('Remove saved settings, upload history, and the stored API key?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
    if Response = IDYES then
    begin
      AppDataPath := ExpandConstant('{userappdata}\VirusTotalOneClick');
      DelTree(AppDataPath, True, True, True);
    end;
  end;
end;
