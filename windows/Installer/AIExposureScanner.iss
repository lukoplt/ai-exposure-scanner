; Inno Setup script for AI Exposure Scanner — Windows installer
;
; Built in CI by .github/workflows/windows-release.yml:
;   ISCC.exe /DAppVersion="0.2.1" windows/Installer/AIExposureScanner.iss
;
; The /DAppVersion flag overrides the AppVersion default below.
; Source paths are relative to this .iss file:
;   ..\..\dist\windows-app  ← framework-dependent publish output
;   ..\..\dist              ← directory the installer .exe lands in
;   ..\..\LICENSE           ← shown in the License page of the wizard

#define AppName       "AI Exposure Scanner"
#define AppShortName  "AIExposureScanner"
#define AppPublisher  "Lukas Oplt"
#define AppURL        "https://github.com/lukoplt/ai-exposure-scanner"
#define AppExeName    "AIExposureScanner.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
; A new stable GUID identifies this product across upgrade installs.
; DO NOT change this once the first release has shipped.
AppId={{F8E5D2F1-8B33-4A2C-B6E1-AEDA6F1E2C50}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppShortName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\..\dist
OutputBaseFilename={#AppShortName}-v{#AppVersion}-windows-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Allow installing per-user (no admin) OR per-machine (admin) — wizard asks.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
LicenseFile=..\..\LICENSE
SetupIconFile=
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "czech";   MessagesFile: "compiler:Languages\Czech.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Pull in every file from the framework-dependent publish output (DLLs,
; runtime config, the main EXE). recursesubdirs handles sub-runtime folders.
Source: "..\..\dist\windows-app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// .NET 10 Desktop Runtime detection. Inspects the registry path that the
// official .NET installers populate. Warns the user if missing but does
// not block the install — they may have installed via a non-standard
// route (winget, chocolatey, manual unzip) that we can't see.

function IsDotNet10DesktopRuntimeInstalled(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM,
      'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
      Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('10.', Names[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
  // Also check the non-WOW6432 path on 64-bit Windows.
  if RegGetSubkeyNames(HKLM,
      'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
      Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('10.', Names[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  Response: Integer;
begin
  Result := True;
  if not IsDotNet10DesktopRuntimeInstalled() then
  begin
    Response := MsgBox(
      'AI Exposure Scanner requires the .NET 10 Desktop Runtime (x64).' + #13#10 + #13#10 +
      'It does not appear to be installed.' + #13#10 + #13#10 +
      'Download it from:' + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/10.0' + #13#10 + #13#10 +
      'Continue installing anyway?',
      mbConfirmation, MB_YESNO);
    Result := (Response = IDYES);
  end;
end;
