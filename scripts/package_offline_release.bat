@echo off
setlocal
cd /d "%~dp0"
echo ===================================================
echo   Packaging 100%% Offline MedTRx Release Bundle...
echo ===================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package_offline_release.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Offline packaging failed with code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)
echo.
if "%1"=="" pause
