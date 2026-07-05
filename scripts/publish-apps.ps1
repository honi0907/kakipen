param(
    [string]$Version = "",
    [switch]$SkipZip,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"

function Get-ProjectVersion {
    param([string]$Root)
    $props = Join-Path $Root "Directory.Build.props"
    if (-not (Test-Path $props)) {
        throw "Directory.Build.props not found: $props"
    }
    $content = Get-Content -Path $props -Raw
    if ($content -match '<Version>\s*([^<\s]+)\s*</Version>') {
        return $Matches[1].Trim()
    }
    throw "Could not read <Version> from Directory.Build.props"
}

function New-PortableZip {
    param(
        [string]$AppFolderName,
        [string]$AppSourceDir,
        [string]$AssetsSourceDir,
        [string]$OutputZipPath
    )

    $staging = Join-Path $dist "_staging_$AppFolderName"
    if (Test-Path $staging) {
        Remove-Item $staging -Recurse -Force
    }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    $appDest = Join-Path $staging $AppFolderName
    Copy-Item -Path $AppSourceDir -Destination $appDest -Recurse -Force
    Copy-Item -Path $AssetsSourceDir -Destination (Join-Path $staging "assets") -Recurse -Force

    if ($AppFolderName -eq "SaveViewer") {
        $launcherBat = Join-Path $appDest "起動_保存一覧.bat"
        @"
@echo off
start "" "%~dp0KakiMoni.Host.exe" --save-viewer
"@ | Set-Content -Path $launcherBat -Encoding ASCII
    }

    if (Test-Path $OutputZipPath) {
        Remove-Item $OutputZipPath -Force
    }
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $OutputZipPath -CompressionLevel Optimal
    Remove-Item $staging -Recurse -Force
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -Root $root
}

Write-Host "KakiMoni publish v$Version"
Write-Host ""

Stop-Process -Name "KakiMoni.Host", "KakiMoni.Client", "KakiMoni.Layout" -Force -ErrorAction SilentlyContinue

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path `
    (Join-Path $dist "Host"), `
    (Join-Path $dist "Client"), `
    (Join-Path $dist "Layout") | Out-Null

$publishArgs = @(
    "publish",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained",
    "-p:Platform=x64"
)

Push-Location (Join-Path $root "src\KakiMoni.Host")
dotnet @publishArgs -o (Join-Path $dist "Host")
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

Push-Location (Join-Path $root "src\KakiMoni.Client")
dotnet @publishArgs -o (Join-Path $dist "Client")
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

Push-Location (Join-Path $root "src\KakiMoni.Layout")
dotnet @publishArgs -o (Join-Path $dist "Layout")
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }
Pop-Location

$priChecks = @(
    @{ Dir = "Host"; Pri = "KakiMoni.Host.pri" },
    @{ Dir = "Client"; Pri = "KakiMoni.Client.pri" },
    @{ Dir = "Layout"; Pri = "KakiMoni.Layout.pri" }
)
foreach ($check in $priChecks) {
    $priPath = Join-Path $dist $check.Dir $check.Pri
    if (-not (Test-Path $priPath)) {
        throw "Missing WinUI resource file (app will not start): $priPath"
    }
}

$assetsSource = Join-Path $root "assets"
if (-not $SkipZip) {
    New-Item -ItemType Directory -Force -Path (Join-Path $dist "assets") | Out-Null
    Copy-Item (Join-Path $assetsSource "*") (Join-Path $dist "assets") -Recurse -Force -ErrorAction SilentlyContinue
}

$zips = @()
if (-not $SkipZip) {
    $zipTargets = @(
        @{ Folder = "Host"; SourceSub = "Host"; Name = "KakiMoni_Host-$Version-Portable.zip" },
        @{ Folder = "Client"; SourceSub = "Client"; Name = "KakiMoni_Client-$Version-Portable.zip" },
        @{ Folder = "Layout"; SourceSub = "Layout"; Name = "KakiMoni_Layout-$Version-Portable.zip" },
        @{ Folder = "SaveViewer"; SourceSub = "Host"; Name = "KakiMoni_SaveViewer-$Version-Portable.zip" }
    )

    foreach ($target in $zipTargets) {
        $zipPath = Join-Path $dist $target.Name
        New-PortableZip `
            -AppFolderName $target.Folder `
            -AppSourceDir (Join-Path $dist $target.SourceSub) `
            -AssetsSourceDir $assetsSource `
            -OutputZipPath $zipPath
        $zips += $zipPath
        Write-Host "ZIP: $zipPath"
    }

    $allStaging = Join-Path $dist "_staging_All"
    if (Test-Path $allStaging) {
        Remove-Item $allStaging -Recurse -Force
    }
    New-Item -ItemType Directory -Path $allStaging -Force | Out-Null
    Copy-Item (Join-Path $dist "Host") (Join-Path $allStaging "Host") -Recurse -Force
    Copy-Item (Join-Path $dist "Client") (Join-Path $allStaging "Client") -Recurse -Force
    Copy-Item (Join-Path $dist "Layout") (Join-Path $allStaging "Layout") -Recurse -Force
    Copy-Item (Join-Path $dist "assets") (Join-Path $allStaging "assets") -Recurse -Force

    $allZip = Join-Path $dist "KakiMoni_All-$Version-Portable.zip"
    if (Test-Path $allZip) {
        Remove-Item $allZip -Force
    }
    Compress-Archive -Path (Join-Path $allStaging "*") -DestinationPath $allZip -CompressionLevel Optimal
    Remove-Item $allStaging -Recurse -Force
    $zips += $allZip
    Write-Host "ZIP: $allZip"
}

$setups = @()
if (-not $SkipInstaller) {
    & (Join-Path $root "installer\build-installers.ps1") -Version $Version -DistDir $dist
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $setups = @(
        (Join-Path $dist "KakiMoni_Host-$Version-Setup.exe"),
        (Join-Path $dist "KakiMoni_Client-$Version-Setup.exe"),
        (Join-Path $dist "KakiMoni_Layout-$Version-Setup.exe"),
        (Join-Path $dist "KakiMoni_SaveViewer-$Version-Setup.exe")
    )
}

$manifest = @{
    version   = $Version
    builtAt   = (Get-Date).ToString("o")
    host      = Join-Path $dist "Host\KakiMoni.Host.exe"
    client    = Join-Path $dist "Client\KakiMoni.Client.exe"
    layout    = Join-Path $dist "Layout\KakiMoni.Layout.exe"
    assets    = if ($SkipZip) { $null } else { Join-Path $dist "assets" }
    portableZips = $zips
    setupInstallers = $setups
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $dist "release-manifest.json") -Encoding UTF8

Write-Host ""
Write-Host "Published v$Version"
Write-Host "  Host:   $dist\Host\KakiMoni.Host.exe"
Write-Host "  Client: $dist\Client\KakiMoni.Client.exe"
Write-Host "  Layout: $dist\Layout\KakiMoni.Layout.exe"
if (-not $SkipZip) {
    Write-Host "  assets: $dist\assets\"
    Write-Host "  Portable ZIPs: $dist\KakiMoni_*-$Version-Portable.zip"
}
if (-not $SkipInstaller) {
    Write-Host "  Setup installers: $dist\KakiMoni_*-$Version-Setup.exe"
}
