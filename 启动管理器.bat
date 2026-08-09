@echo off
setlocal
title MelonModifier Launcher

set "EXE=%~dp0src/MelonModifier.App/bin/Debug/net8.0-windows/MelonModifier.App.exe"

if not exist "%EXE%" (
    echo [MelonModifier] compiled binary not found, building now...
    cd /d "%~dp0"
    dotnet build MelonModifier.sln -v q
    if errorlevel 1 (
        echo [MelonModifier] BUILD FAILED - check errors above.
        pause
        exit /b 1
    )
)

echo [MelonModifier] launching...
start "" "%EXE%"
