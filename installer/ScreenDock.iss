#ifndef AppVersion
#define AppVersion "1.0.0"
#endif
[Setup]
AppId={{26C69994-C754-4314-820D-3E207BEAAB68}
AppName=ScreenDock
AppVersion={#AppVersion}
AppPublisher=penghanrose-coder
DefaultDirName={localappdata}\Programs\ScreenDock
DefaultGroupName=ScreenDock
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\..
OutputBaseFilename=ScreenDock-Setup-{#AppVersion}-x64
SetupIconFile=..\ScreenDock.ico
UninstallDisplayIcon={app}\ScreenDock.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
AppMutex=Local\ScreenDock.Desktop
CloseApplications=no
RestartApplications=no
LicenseFile=..\LICENSE
[Languages]
Name: "zhcn"; MessagesFile: "ChineseSimplified.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
Name: "startup"; Description: "登录 Windows 后自动启动（进入托盘）"; Flags: unchecked
[Files]
Source: "..\ScreenDock.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ScreenDock.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\使用说明.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\profiles\monitors.xml"; DestDir: "{app}\profiles"; Flags: ignoreversion
[Icons]
Name: "{group}\ScreenDock"; Filename: "{app}\ScreenDock.exe"; WorkingDir: "{app}"
Name: "{group}\卸载 ScreenDock"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ScreenDock"; Filename: "{app}\ScreenDock.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\ScreenDock.exe"; Description: "打开 ScreenDock"; Flags: nowait postinstall skipifsilent
[Code]
const RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
var HadStartup: Boolean;
function InitializeSetup: Boolean;
begin
  HadStartup := RegValueExists(HKCU, RunKey, 'ScreenDock');
  Result := True;
end;
procedure InitializeWizard;
begin
  if RegValueExists(HKCU, RunKey, 'ScreenDock') then
    WizardSelectTasks('startup');
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('startup') or (WizardSilent and HadStartup) then
      RegWriteStringValue(HKCU, RunKey, 'ScreenDock', '"' + ExpandConstant('{app}\ScreenDock.exe') + '" --tray')
    else
      RegDeleteValue(HKCU, RunKey, 'ScreenDock');
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, RunKey, 'ScreenDock', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\ScreenDock.exe') + '" --tray') = 0 then
        RegDeleteValue(HKCU, RunKey, 'ScreenDock');
  { User presets and eye-comfort recovery data are intentionally preserved. }
end;
