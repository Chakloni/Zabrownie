[Setup]
AppName=Zabrownie Browser
AppVersion=0.1
DefaultDirName={pf}\Zabrownie
DefaultGroupName=Zabrownie
OutputBaseFilename=ZabrownieSetup
Compression=lzma
SolidCompression=yes
SetupIconFile=Assets\Zabrownie.ico

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\Zabrownie"; Filename: "{app}\Zabrownie.exe"
Name: "{commondesktop}\Zabrownie"; Filename: "{app}\Zabrownie.exe"
