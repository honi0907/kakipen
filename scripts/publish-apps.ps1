param(
    [string]$Version = "",
    [ValidateSet("Host", "Client", "Layout", "SaveViewer", "All")]
    [string[]]$Target = @("All"),
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

function Resolve-PublishTargets {
    param([string[]]$Names)
    if ($Names -contains "All") {
        return @("Host", "Client", "Layout")
    }

    $publish = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $Names) {
        switch ($name) {
            "Host" { if (-not $publish.Contains("Host")) { $publish.Add("Host") } }
            "Client" { if (-not $publish.Contains("Client")) { $publish.Add("Client") } }
            "Layout" { if (-not $publish.Contains("Layout")) { $publish.Add("Layout") } }
            "SaveViewer" { if (-not $publish.Contains("Host")) { $publish.Add("Host") } }
        }
    }

    if ($publish.Count -eq 0) {
        throw "No publish target resolved. Use -Target Host, Client, Layout, SaveViewer, or All."
    }

    return @($publish)
}

function Resolve-InstallerTargets {
    param([string[]]$Names)
    if ($Names -contains "All") {
        return @("Host", "Client", "Layout", "SaveViewer")
    }

    $installers = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $Names) {
        switch ($name) {
            "Host" {
                if (-not $installers.Contains("Host")) { $installers.Add("Host") }
                if (-not $installers.Contains("SaveViewer")) { $installers.Add("SaveViewer") }
            }
            "Client" { if (-not $installers.Contains("Client")) { $installers.Add("Client") } }
            "Layout" { if (-not $installers.Contains("Layout")) { $installers.Add("Layout") } }
            "SaveViewer" { if (-not $installers.Contains("SaveViewer")) { $installers.Add("SaveViewer") } }
        }
    }

    if ($installers.Count -eq 0) {
        throw "No installer target resolved. Use -Target Host, Client, Layout, SaveViewer, or All."
    }

    return @($installers)
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

$publishTargets = Resolve-PublishTargets -Names $Target
$installerTargets = Resolve-InstallerTargets -Names $Target
$fullRelease = $Target -contains "All"

Write-Host "KakiMoni publish v$Version"
Write-Host "Targets: $($Target -join ', ')"
Write-Host "Publish: $($publishTargets -join ', ')"
if (-not $SkipInstaller) {
    Write-Host "Installers: $($installerTargets -join ', ')"
}
Write-Host ""

Stop-Process -Name "KakiMoni.Host", "KakiMoni.Client", "KakiMoni.Layout" -Force -ErrorAction SilentlyContinue

if ($fullRelease) {
    Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
} else {
    foreach ($name in $publishTargets) {
        Remove-Item (Join-Path $dist $name) -Recurse -Force -ErrorAction SilentlyContinue
    }
    foreach ($name in $installerTargets) {
        $setup = Join-Path $dist "KakiMoni_${name}-$Version-Setup.exe"
        if (Test-Path $setup) {
            Remove-Item $setup -Force
        }
    }
}

foreach ($name in $publishTargets) {
    New-Item -ItemType Directory -Force -Path (Join-Path $dist $name) | Out-Null
}

$publishArgs = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained",
    "-p:Platform=x64"
)

$projectMap = @{
    Host   = "src\KakiMoni.Host\KakiMoni.Host.csproj"
    Client = "src\KakiMoni.Client\KakiMoni.Client.csproj"
    Layout = "src\KakiMoni.Layout\KakiMoni.Layout.csproj"
}

$priMap = @{
    Host   = "KakiMoni.Host.pri"
    Client = "KakiMoni.Client.pri"
    Layout = "KakiMoni.Layout.pri"
}

foreach ($name in $publishTargets) {
    $project = Join-Path $root $projectMap[$name]
    $outDir = Join-Path $dist $name
    Write-Host "dotnet publish $name ..."
    dotnet publish $project @publishArgs -o $outDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $priPath = Join-Path $outDir $priMap[$name]
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
    $zipDefs = @(
        @{ Key = "Host"; Folder = "Host"; SourceSub = "Host"; Name = "KakiMoni_Host-$Version-Portable.zip" },
        @{ Key = "Client"; Folder = "Client"; SourceSub = "Client"; Name = "KakiMoni_Client-$Version-Portable.zip" },
        @{ Key = "Layout"; Folder = "Layout"; SourceSub = "Layout"; Name = "KakiMoni_Layout-$Version-Portable.zip" },
        @{ Key = "SaveViewer"; Folder = "SaveViewer"; SourceSub = "Host"; Name = "KakiMoni_SaveViewer-$Version-Portable.zip" }
    )

    foreach ($zipDef in $zipDefs) {
        if ($installerTargets -notcontains $zipDef.Key) { continue }
        $zipPath = Join-Path $dist $zipDef.Name
        New-PortableZip `
            -AppFolderName $zipDef.Folder `
            -AppSourceDir (Join-Path $dist $zipDef.SourceSub) `
            -AssetsSourceDir $assetsSource `
            -OutputZipPath $zipPath
        $zips += $zipPath
        Write-Host "ZIP: $zipPath"
    }

    if ($fullRelease) {
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
}

$setups = @()
if (-not $SkipInstaller) {
    & (Join-Path $root "installer\build-installers.ps1") -Version $Version -DistDir $dist -InstallerTargets $installerTargets
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    foreach ($name in $installerTargets) {
        $setups += (Join-Path $dist "KakiMoni_${name}-$Version-Setup.exe")
    }
}

$manifest = @{
    version         = $Version
    builtAt         = (Get-Date).ToString("o")
    targets         = @($Target)
    publishTargets  = $publishTargets
    installerTargets = $installerTargets
    host            = Join-Path $dist "Host\KakiMoni.Host.exe"
    client          = Join-Path $dist "Client\KakiMoni.Client.exe"
    layout          = Join-Path $dist "Layout\KakiMoni.Layout.exe"
    assets          = if ($SkipZip) { $null } else { Join-Path $dist "assets" }
    portableZips    = $zips
    setupInstallers = $setups
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $dist "release-manifest.json") -Encoding UTF8

Write-Host ""
Write-Host "Published v$Version"
foreach ($name in $publishTargets) {
    $exe = Join-Path $dist "$name\KakiMoni.$name.exe"
    Write-Host "  $name : $exe"
}
if (-not $SkipZip -and $zips.Count -gt 0) {
    Write-Host "  Portable ZIPs: $($zips.Count) file(s)"
}
if (-not $SkipInstaller) {
    Write-Host "  Setup installers: $($setups.Count) file(s)"
}
