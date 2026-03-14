[Setup]
AppId={{c4b97379-bfe5-48db-addd-1f5c4d3044f0}
AppName=Better Window Switcher
AppVersion=1.1
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
DefaultDirName={autopf}\bws
DefaultGroupName=bws
PrivilegesRequired=admin
UninstallDisplayIcon={app}\bws.exe
Compression=lzma2
SolidCompression=yes
OutputDir=.\InstallerOutput
OutputBaseFilename=bws_setup

[Tasks]
Name: "startup"; Description: "Launch Better Window Switcher on Windows startup (Silent Admin)"; GroupDescription: "Additional settings:";

[Files]
Source: "bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Better Window Switcher"; Filename: "{app}\bws.exe"
Name: "{commondesktop}\Better Window Switcher"; Filename: "{app}\bws.exe"

[Run]
Filename: "{app}\bws.exe"; \
    Description: "Launch Better Window Switcher"; \
    Flags: nowait postinstall skipifsilent shellexec; \
    WorkingDir: "{app}"

Filename: "schtasks"; \
    Parameters: "/Create /tn ""bws_startup"" /tr ""'{app}\bws.exe'"" /sc onlogon /rl highest /f"; \
    Flags: runhidden; Tasks: startup

[UninstallRun]
Filename: "schtasks"; Parameters: "/Delete /tn ""bws_startup"" /f"; Flags: runhidden