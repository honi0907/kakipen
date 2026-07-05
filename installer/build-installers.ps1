param(
    [string]$Version = "",
    [string]$DistDir = "",
    [string]$IsccPath = "",
    [ValidateSet("Host", "Client", "Layout", "SaveViewer")]
    [string[]]$InstallerTargets = @("Host", "Client", "Layout", "SaveViewer")
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = if ([string]::IsNullOrWhiteSpace($DistDir)) { Join-Path $root "dist" } else { $DistDir }

function Get-ProjectVersion {
    param([string]$Root)
    $props = Join-Path $Root "Directory.Build.props"
    $content = Get-Content -Path $props -Raw
    if ($content -match '<Version>\s*([^<\s]+)\s*</Version>') {
        return $Matches[1].Trim()
    }
    throw "Could not read <Version> from Directory.Build.props"
}

function Find-IsccPath {
    param([string]$Override)
    if (-not [string]::IsNullOrWhiteSpace($Override) -and (Test-Path $Override)) {
        return (Resolve-Path $Override).Path
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return (Resolve-Path $path).Path
        }
    }

    return $null
}

function Ensure-InnoSetup {
    param([string]$IsccOverride)

    $iscc = Find-IsccPath -Override $IsccOverride
    if ($iscc) {
        return $iscc
    }

    Write-Host "Inno Setup 6 not found. Trying winget install..."
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "Inno Setup 6 (ISCC.exe) not found. Install from https://jrsoftware.org/isdl.php or: winget install --id JRSoftware.InnoSetup -e"
    }

    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
    $iscc = Find-IsccPath -Override $IsccOverride
    if (-not $iscc) {
        throw "ISCC.exe still not found after winget install. Reopen shell or pass -IsccPath."
    }

    return $iscc
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -Root $root
}

if (-not (Test-Path $dist)) {
    throw "dist folder not found. Run scripts/publish-apps.ps1 first: $dist"
}

$assetsDir = Join-Path $root "assets"
if (-not (Test-Path $assetsDir)) {
    throw "assets folder not found: $assetsDir"
}

$iscc = Ensure-InnoSetup -IsccOverride $IsccPath
$installerDir = $PSScriptRoot

$jobs = @(
    @{ Key = "Host"; Iss = "KakiMoni.Host.iss"; PublishSub = "Host" },
    @{ Key = "Client"; Iss = "KakiMoni.Client.iss"; PublishSub = "Client" },
    @{ Key = "Layout"; Iss = "KakiMoni.Layout.iss"; PublishSub = "Layout" },
    @{ Key = "SaveViewer"; Iss = "KakiMoni.SaveViewer.iss"; PublishSub = "Host" }
) | Where-Object { $InstallerTargets -contains $_.Key }

if ($jobs.Count -eq 0) {
    throw "No installer targets selected."
}

$setups = @()
foreach ($job in $jobs) {
    $publishDir = Join-Path $dist $job.PublishSub
    if (-not (Test-Path (Join-Path $publishDir "*.exe"))) {
        throw "Publish output not found: $publishDir"
    }

    $issPath = Join-Path $installerDir $job.Iss
    $defines = @(
        "/DAppVersion=$Version",
        "/DPublishDir=$publishDir",
        "/DAssetsDir=$assetsDir",
        "/DOutputDir=$dist",
        "/DRepoRoot=$root"
    )

    Write-Host "ISCC $($job.Iss) ..."
    & $iscc $defines $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup build failed: $($job.Iss)"
    }

    $setupPath = Join-Path $dist "KakiMoni_$($job.Key)-$Version-Setup.exe"
    if (-not (Test-Path $setupPath)) {
        throw "Setup was not generated: $setupPath"
    }

    $setups += $setupPath
    Write-Host "Setup: $setupPath"
}

Write-Host ""
Write-Host "Installers v$Version"
foreach ($setup in $setups) {
    Write-Host "  $setup"
}

exit 0
