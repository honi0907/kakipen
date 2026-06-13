$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"

Stop-Process -Name "KakiMoni.Host","KakiMoni.Client" -Force -ErrorAction SilentlyContinue

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $dist "Host"), (Join-Path $dist "Client"), (Join-Path $dist "assets") | Out-Null

Push-Location (Join-Path $root "src\KakiMoni.Host")
dotnet publish -c Release -r win-x64 --self-contained -p:Platform=x64 -o (Join-Path $dist "Host")
Pop-Location

Push-Location (Join-Path $root "src\KakiMoni.Client")
dotnet publish -c Release -r win-x64 --self-contained -p:Platform=x64 -o (Join-Path $dist "Client")
Pop-Location

Copy-Item (Join-Path $root "assets\*") (Join-Path $dist "assets") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Published:"
Write-Host "  Host:   $dist\Host\KakiMoni.Host.exe"
Write-Host "  Client: $dist\Client\KakiMoni.Client.exe"
Write-Host "  assets: $dist\assets\"
