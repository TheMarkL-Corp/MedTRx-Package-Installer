@echo off
setlocal
cd /d "%~dp0"
echo ===================================================
echo   Installing MedTRx Application...
echo ===================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Installation failed.
    pause
    exit /b %ERRORLEVEL%
)
echo.
if "%1"=="" pause
