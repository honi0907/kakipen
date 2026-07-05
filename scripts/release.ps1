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
    Write-Host "WithPortableZip: ZIP also generated for local verification"
} else {
    Write-Host "Default: Setup installers only (fast release build)"
}
Write-Host ""

$publishArgs = @("-SkipZip")
if ($WithPortableZip) { $publishArgs = @() }
if ($SkipInstaller) { $publishArgs += "-SkipInstaller" }
& (Join-Path $PSScriptRoot "publish-apps.ps1") @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dist = Join-Path $root "dist"

$releaseNotes = @{
    Host       = @{ Tag = "host-v$version"; Title = "KakiMoni Host v$version" }
    Client     = @{ Tag = "client-v$version"; Title = "KakiMoni Client v$version" }
    Layout     = @{ Tag = "layout-v$version"; Title = "KakiMoni Layout v$version" }
    SaveViewer = @{ Tag = "saveviewer-v$version"; Title = "KakiMoni Save Viewer v$version" }
}

if ($DryRun) {
    Write-Host ""
    Write-Host "DryRun complete. GitHub release not executed."
    exit 0
}

Write-Host ""
Write-Host "Pushing commit..."
git push origin master
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

foreach ($key in @("Host", "Client", "Layout", "SaveViewer")) {
    $setup = Join-Path $dist "KakiMoni_${key}-$version-Setup.exe"
    $meta = $releaseNotes[$key]
    if (-not (Test-Path $setup)) {
        throw "Setup not found: $setup"
    }

    Write-Host ""
    Write-Host "Creating release $($meta.Tag) ..."
    gh release create $meta.Tag $setup --title $meta.Title --notes "WinUI 3 $($meta.Title)"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ""
Write-Host "Done. Releases:"
foreach ($key in @("Host", "Client", "Layout", "SaveViewer")) {
    $tag = $releaseNotes[$key].Tag
    Write-Host "  https://github.com/honi0907/kakipen/releases/tag/$tag"
}
