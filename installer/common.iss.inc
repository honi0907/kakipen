#ifndef AppKey
  #error Define AppKey
#endif
#ifndef AppExe
  #error Define AppExe
#endif
#ifndef AppDisplayName
  #error Define AppDisplayName
#endif
#ifndef AppId
  #error Define AppId
#endif
#ifndef AppFolderName
  #error Define AppFolderName
#endif
#ifndef IconFile
  #error Define IconFile
#endif
#ifndef AppVersion
  #error Define AppVersion via ISCC /DAppVersion=
#endif
#ifndef PublishDir
  #error Define PublishDir via ISCC /DPublishDir=
#endif
#ifndef AssetsDir
  #error Define AssetsDir via ISCC /DAssetsDir=
#endif
#ifndef OutputDir
  #error Define OutputDir via ISCC /DOutputDir=
#endif
#ifndef LaunchParameters
  #define LaunchParameters ""
#endif

[Setup]
AppId={#AppId}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppVerName={#AppDisplayName} {#AppVersion}
AppPublisher=KakiMoni
DefaultDirName={autopf}\{#AppFolderName}
DefaultGroupName={#AppDisplayName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=KakiMoni_{#AppKey}-{#AppVersion}-Setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
ShowLanguageDialog=no
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加タスク:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 初回のみ同梱。既存ファイルは上書きしない（更新でカスタム assets が消えない）
Source: "{#AssetsDir}\*"; DestDir: "{app}\assets"; Flags: ignoreversion recursesubdirs createallsubdirs onlyifdoesntexist

[Icons]
Name: "{group}\{#AppDisplayName}"; Filename: "{app}\{#AppExe}"; Parameters: "{#LaunchParameters}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#AppExe}"; Parameters: "{#LaunchParameters}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "{#LaunchParameters}"; Description: "{#AppDisplayName} を起動する"; Flags: nowait postinstall skipifsilent
