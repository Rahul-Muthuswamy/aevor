[Setup]
AppName=Aevor
AppVersion=1.0.0
AppPublisher=Rahul-Muthuswamy
AppPublisherURL=https://github.com/Rahul-Muthuswamy/aevor
AppSupportURL=https://github.com/Rahul-Muthuswamy/aevor/issues
AppUpdatesURL=https://github.com/Rahul-Muthuswamy/aevor/releases
DefaultDirName={autopf}\Aevor
DefaultGroupName=Aevor
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=AevorSetup-v1.0.0
SetupIconFile=..\src\aevor_ui\assets\aevor.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\win-x64\Aevor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Aevor"; Filename: "{app}\Aevor.exe"
Name: "{group}\Uninstall Aevor"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Aevor"; Filename: "{app}\Aevor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Aevor.exe"; Description: "Launch Aevor"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
