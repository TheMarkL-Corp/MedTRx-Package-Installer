@echo off
setlocal
cd /d "%~dp0"
echo ===================================================
echo   Uninstalling MedTRx Application...
echo ===================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Uninstall encountered an issue.
    pause
    exit /b %ERRORLEVEL%
)
echo.
if "%1"=="" pause
