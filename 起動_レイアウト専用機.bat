@echo off
set EXE=%~dp0src\KakiMoni.Layout\bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\KakiMoni.Layout.exe
if not exist "%EXE%" (
  echo ビルドされていません: KakiMoni.Layout
  echo .\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Layout
  pause
  exit /b 1
)
start "" "%EXE%"
