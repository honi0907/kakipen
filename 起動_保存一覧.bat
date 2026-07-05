@echo off

cd /d "%~dp0"

set "EXE=%~dp0src\KakiMoni.Host\bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\KakiMoni.Host.exe"

if not exist "%EXE%" (

    echo.

    echo [エラー] Host がビルドされていません。

    echo 次を実行してから再度お試しください:

    echo   .\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Host

    echo.

    pause

    exit /b 1

)

if defined SERVER_URL (

    start "" "%EXE%" --save-viewer --server "%SERVER_URL%"

) else (

    start "" "%EXE%" --save-viewer

)

