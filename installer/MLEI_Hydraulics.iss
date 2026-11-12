#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MLEI Group"

[Setup]
; Unique ID of this installer (not the same as the Revit AddInId). Never change it.
AppId={{62C50A82-21EB-43B7-9CD9-C4A2E5944FF2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Per-user install: no admin rights needed
PrivilegesRequired=lowest
DefaultDirName={localappdata}\MLEI_Hydraulics
DisableDirPage=yes
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=MLEI_Hydraulics_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}

[Files]
Source: "..\MLEI_Hydraulics\bin\Release\R2025\MLEI_Hydraulics.dll"; DestDir: "{app}\2025"; Flags: ignoreversion; Check: IsRevitInstalled('2025')
Source: "..\MLEI_Hydraulics\bin\Release\R2025\MLEI_Hydraulics.deps.json"; DestDir: "{app}\2025"; Flags: ignoreversion; Check: IsRevitInstalled('2025')
Source: "..\MLEI_Hydraulics\bin\Release\R2026\MLEI_Hydraulics.dll"; DestDir: "{app}\2026"; Flags: ignoreversion; Check: IsRevitInstalled('2026')
Source: "..\MLEI_Hydraulics\bin\Release\R2026\MLEI_Hydraulics.deps.json"; DestDir: "{app}\2026"; Flags: ignoreversion; Check: IsRevitInstalled('2026')

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\MLEI_Hydraulics.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\MLEI_Hydraulics.addin"

[Code]
const
  RevitAddInId = '8D927970-F439-47BF-BD57-1EFDACBEDE1A';

function AddInFolder(Year: string): string;
begin
  Result := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\') + Year;
end;

function IsRevitInstalled(Year: string): Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{commonpf64}\Autodesk\Revit ') + Year) or
    DirExists(AddInFolder(Year));
end;

function InitializeSetup(): Boolean;
begin
  Result := IsRevitInstalled('2025') or IsRevitInstalled('2026');
  if not Result then
    MsgBox('No Revit 2025 or Revit 2026 installation was found on this computer.', mbError, MB_OK);
end;

procedure WriteAddIn(Year: string);
var
  Xml: string;
  Lines: TArrayOfString;
begin
  Xml :=
    '<?xml version="1.0" encoding="utf-8" standalone="no"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>MLEI Hydraulics</Name>' + #13#10 +
    '    <Assembly>' + ExpandConstant('{app}\') + Year + '\MLEI_Hydraulics.dll</Assembly>' + #13#10 +
    '    <FullClassName>MLEI_Hydraulics.App</FullClassName>' + #13#10 +
    '    <AddInId>' + RevitAddInId + '</AddInId>' + #13#10 +
    '    <VendorId>MLEI</VendorId>' + #13#10 +
    '    <VendorDescription>Hydraulics Tools for Revit</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>';

  SetArrayLength(Lines, 1);
  Lines[0] := Xml;
  ForceDirectories(AddInFolder(Year));
  SaveStringsToUTF8File(AddInFolder(Year) + '\MLEI_Hydraulics.addin', Lines, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if IsRevitInstalled('2025') then WriteAddIn('2025');
    if IsRevitInstalled('2026') then WriteAddIn('2026');
  end;
end;