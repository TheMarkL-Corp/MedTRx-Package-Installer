@echo off
setlocal
cd /d "%~dp0"
echo ===================================================
echo   Packaging MedTRx Release...
echo ===================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package_release.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Packaging failed.
    pause
    exit /b %ERRORLEVEL%
)
echo.
if "%1"=="" pause
