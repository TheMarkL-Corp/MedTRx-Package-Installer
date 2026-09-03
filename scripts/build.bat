@echo off
setlocal
cd /d "%~dp0"
echo Starting MedTRx Build Process...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed with code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo [SUCCESS] Build finished cleanly!
if "%1"=="" pause
