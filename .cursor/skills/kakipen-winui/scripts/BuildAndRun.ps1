# KakiMoni WinUI — build & optional launch (x64)
param(
    [ValidateSet('Host', 'Client', 'Both', 'Solution')]
    [string]$Target = 'Solution',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Run,
    [switch]$StopRunning
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
$platform = 'x64'
$tfn = 'net8.0-windows10.0.26100.0'
$rid = 'win-x64'

function Stop-KakiMoni {
    Stop-Process -Name 'KakiMoni.Host', 'KakiMoni.Client' -Force -ErrorAction SilentlyContinue
}

function Build-Project([string]$ProjectDir) {
    Push-Location $ProjectDir
    try {
        dotnet build -c $Configuration -p:Platform=$platform
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $ProjectDir" }
    }
    finally {
        Pop-Location
    }
}

function Get-ExePath([string]$ProjectName) {
    Join-Path $root "src\$ProjectName\bin\$platform\$Configuration\$tfn\$rid\$ProjectName.exe"
}

if ($StopRunning -or $Run) { Stop-KakiMoni }

switch ($Target) {
    'Solution' {
        # sln は Debug|Any CPU（Host/Client は Any CPU 経由で x64 にマップ）
        dotnet build (Join-Path $root 'KakiMoni.WinUI.sln') -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    'Both' {
        Build-Project (Join-Path $root 'src\KakiMoni.Host')
        Build-Project (Join-Path $root 'src\KakiMoni.Client')
    }
    'Host' {
        Build-Project (Join-Path $root 'src\KakiMoni.Host')
        if ($Run) {
            $exe = Get-ExePath 'KakiMoni.Host'
            if (-not (Test-Path $exe)) { throw "Not found: $exe" }
            $p = Start-Process -FilePath $exe -PassThru
            Write-Host "KakiMoni.Host launched (PID: $($p.Id))"
        }
    }
    'Client' {
        Build-Project (Join-Path $root 'src\KakiMoni.Client')
        if ($Run) {
            $exe = Get-ExePath 'KakiMoni.Client'
            if (-not (Test-Path $exe)) { throw "Not found: $exe" }
            $p = Start-Process -FilePath $exe -PassThru
            Write-Host "KakiMoni.Client launched (PID: $($p.Id))"
        }
    }
}

if ($Run -and $Target -eq 'Both') {
    $hostExe = Get-ExePath 'KakiMoni.Host'
    $clientExe = Get-ExePath 'KakiMoni.Client'
    foreach ($exe in @($hostExe, $clientExe)) {
        if (-not (Test-Path $exe)) { throw "Not found: $exe" }
    }
    $hp = Start-Process -FilePath $hostExe -PassThru
    Start-Sleep -Seconds 1
    $cp = Start-Process -FilePath $clientExe -PassThru
    Write-Host "KakiMoni.Host launched (PID: $($hp.Id))"
    Write-Host "KakiMoni.Client launched (PID: $($cp.Id))"
}

Write-Host "Done ($Target, $Configuration, $platform)"
