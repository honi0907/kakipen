param(
    [switch]$WithPortableZip,
    [switch]$SkipInstaller,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$props = Join-Path $root "Directory.Build.props"
$content = Get-Content -Path $props -Raw
if ($content -notmatch '<Version>\s*([^<\s]+)\s*</Version>') {
    throw "Could not read <Version> from Directory.Build.props"
}
$version = $Matches[1].Trim()

Write-Host "Release prep v$version"
if ($WithPortableZip) {
    Write-Host "(WithPortableZip: ZIP also generated for local verification)"
} else {
    Write-Host "(default: Setup installers only — fast release build)"
}
Write-Host ""

$publishArgs = @("-SkipZip")
if ($WithPortableZip) { $publishArgs = @() }
if ($SkipInstaller) { $publishArgs += "-SkipInstaller" }
& (Join-Path $PSScriptRoot "publish-apps.ps1") @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dist = Join-Path $root "dist"
$setups = @(
    "KakiMoni_Host-$version-Setup.exe",
    "KakiMoni_Client-$version-Setup.exe",
    "KakiMoni_Layout-$version-Setup.exe"
) | ForEach-Object { Join-Path $dist $_ }

Write-Host ""
Write-Host "Next steps (manual):"
Write-Host "  1. Setup を別フォルダにインストールし、親機→子機の疎通を確認"
Write-Host "  2. commit → 各アプリの tag を push"
Write-Host ""
Write-Host "GitHub Release（アプリごとに個別。Setup のみ公開）:"

$releaseNotes = @{
    Host       = @{ Tag = "host-v$version"; Title = "KakiMoni Host v$version" }
    Client     = @{ Tag = "client-v$version"; Title = "KakiMoni Client v$version" }
    Layout     = @{ Tag = "layout-v$version"; Title = "KakiMoni Layout v$version" }
    SaveViewer = @{ Tag = "saveviewer-v$version"; Title = "KakiMoni Save Viewer v$version" }
}

foreach ($key in @("Host", "Client", "Layout", "SaveViewer")) {
    $setup = Join-Path $dist "KakiMoni_${key}-$version-Setup.exe"
    $meta = $releaseNotes[$key]
    Write-Host ""
    Write-Host "  gh release create $($meta.Tag) \"
    if (Test-Path $setup) {
        Write-Host "    `"$setup`" \"
    }
    Write-Host "    --title `"$($meta.Title)`" \"
    Write-Host "    --notes `"WinUI 3 $($meta.Title)`""
}

Write-Host ""
if ($WithPortableZip) {
    Write-Host "Portable ZIP generated for local use only (not uploaded to GitHub)."
} else {
    Write-Host "Portable ZIP skipped (use -WithPortableZip only when instructed)."
}

if ($DryRun) {
    Write-Host ""
    Write-Host "(DryRun: tag / gh release not executed)"
    exit 0
}
