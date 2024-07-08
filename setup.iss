#define MyAppName "AutoTotal"
#define MyAppVersion "1.1"
#define MyAppPublisher "malw.ru"
#define MyAppURL "https://malw.ru/autototal"
#define MyAppExeName "AutoTotal.exe"

[Setup]
AppId={{F8A5E29D-B6A8-4696-941C-FDFA8ABD44F8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=C:\Users\MALWARE\Desktop
OutputBaseFilename=AutoTotalSetup
SetupIconFile=C:\Users\MALWARE\Documents\AutoTotal\atsetup.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\AutoTotal.exe
 
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
CreateStartMenuShortcut=Create Start Menu shortcut
russian.CreateStartMenuShortcut=Создать ярлык в меню Пуск
AddScanToContextMenu=Add "Scan on VirusTotal" in files' context menu
russian.AddScanToContextMenu=Добавить элемент "Сканировать на VirusTotal" в контекстное меню файлов
ScanOnVT=Scan on VirusTotal
russian.ScanOnVT=Сканировать на VirusTotal
DontEnterCyrillicLetters=You can't install AutoTotal in folder with cyrillic letters in it
russian.DontEnterCyrillicLetters=Нельзя установить AutoTotal в папку с русскими буквами

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "addcontextmenu"; Description: "{cm:AddScanToContextMenu}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "{cm:CreateStartMenuShortcut}"; GroupDescription: "{cm:AdditionalIcons}"

[Registry]
Root: HKCR; Subkey: "*\shell\AutoTotal"; ValueType: string; ValueName: ""; ValueData: "{cm:ScanOnVT}"; Tasks: addcontextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\AutoTotal"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: addcontextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\AutoTotal\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" /scan ""%1"""; Tasks: addcontextmenu; Flags: uninsdeletekey

[Files]
Source: "C:\Users\MALWARE\Documents\AutoTotal\bin\Release\net7.0-windows10.0.19041.0\win-x64\publish\*"; Excludes: "AutoTotal.pdb, AutoTotal.runtimeconfig.json, createdump.exe"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsCyrillicString(const S: String): Boolean;
var
  CharIndex: Integer;
begin
  Result := False;
  for CharIndex := 1 to Length(S) do
  begin
    if (S[CharIndex] >= #$0400) and (S[CharIndex] <= #$04FF) then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if IsCyrillicString(WizardForm.DirEdit.Text) then
    begin
      MsgBox(CustomMessage('DontEnterCyrillicLetters'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;


