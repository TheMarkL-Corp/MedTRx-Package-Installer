<#
.SYNOPSIS
    Uninstalls MedTRx Application:
    - Closes running MedTRx instances
    - Removes Desktop and Start Menu shortcuts
    - Removes registry Add/Remove entry
    - Cleans up installed files in %LOCALAPPDATA%\Programs\MedTRx
#>

[CmdletBinding()]
param(
    [switch]$KeepUserData,
    [switch]$Silent
)

$ErrorActionPreference = "SilentlyContinue"

function Write-Step([string]$msg) {
    if (-not $Silent) { Write-Host "[*] $msg" -ForegroundColor Cyan }
}

function Write-Success([string]$msg) {
    if (-not $Silent) { Write-Host "[+] $msg" -ForegroundColor Green }
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx Application Uninstaller                 " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Terminate running instances
Write-Step "Terminating running MedTRx processes..."
Get-Process -Name "MedTRx" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Remove Shortcuts
Write-Step "Removing shortcuts..."
$DesktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "MedTRx.lnk"
if (Test-Path $DesktopShortcut) {
    Remove-Item $DesktopShortcut -Force
    Write-Success "Removed Desktop shortcut."
}

$StartMenuShortcut = Join-Path ([Environment]::GetFolderPath("Programs")) "MedTRx.lnk"
if (Test-Path $StartMenuShortcut) {
    Remove-Item $StartMenuShortcut -Force
    Write-Success "Removed Start Menu shortcut."
}

# 3. Remove Registry Entry
Write-Step "Removing registry uninstall entry..."
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MedTRx"
if (Test-Path $UninstallKey) {
    Remove-Item $UninstallKey -Recurse -Force
    Write-Success "Removed Windows Installed Apps entry."
}

# 4. Remove Installed Program Files
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\MedTRx"
if (Test-Path $InstallDir) {
    Write-Step "Removing installed program files from $InstallDir..."
    Remove-Item $InstallDir -Recurse -Force
    Write-Success "Removed program files."
}

# 5. Clean User Data if not requested to keep
if (-not $KeepUserData) {
    $AppDataDir = Join-Path $env:LOCALAPPDATA "MedTRx"
    if (Test-Path $AppDataDir) {
        Write-Step "Cleaning application cache & user data..."
        Remove-Item $AppDataDir -Recurse -Force
        Write-Success "Removed cached user data."
    }
}

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  UNINSTALL COMPLETED SUCCESSFULLY!              " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
