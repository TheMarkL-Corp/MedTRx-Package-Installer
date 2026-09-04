<#
.SYNOPSIS
    Installs MedTRx Application for current user:
    - Deploys to %LOCALAPPDATA%\Programs\MedTRx
    - Creates Desktop shortcut with custom icon
    - Creates Start Menu shortcut
    - Registers Windows Installed Apps (Add/Remove Programs) entry
    - Attempts Taskbar pinning
#>

[CmdletBinding()]
param(
    [switch]$Silent
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) {
    if (-not $Silent) {
        Write-Host "[*] $msg" -ForegroundColor Cyan
    }
}

function Write-Success([string]$msg) {
    if (-not $Silent) {
        Write-Host "[+] $msg" -ForegroundColor Green
    }
}

function Write-WarnMsg([string]$msg) {
    if (-not $Silent) {
        Write-Host "[!] $msg" -ForegroundColor Yellow
    }
}

function Test-WebView2Installed {
    $regPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    )
    foreach ($r in $regPaths) {
        if (Test-Path $r) {
            $pv = (Get-ItemProperty -Path $r -ErrorAction SilentlyContinue).pv
            if (-not [string]::IsNullOrEmpty($pv) -and $pv -ne "0.0.0.0") {
                return $true
            }
        }
    }
    return $false
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx Application Installer                   " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Determine Source Files Location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SourceDir = $ScriptDir

if (Test-Path (Join-Path $ScriptDir "MedTRx.exe")) {
    $SourceDir = $ScriptDir
} elseif (Test-Path (Join-Path $ScriptDir "..\dist\MedTRx.exe")) {
    $SourceDir = (Resolve-Path (Join-Path $ScriptDir "..\dist")).Path
} elseif (Test-Path (Join-Path $ScriptDir "dist\MedTRx.exe")) {
    $SourceDir = (Resolve-Path (Join-Path $ScriptDir "dist")).Path
} else {
    Write-Error "Could not find compiled MedTRx.exe. Please run build.bat first!"
    exit 1
}

Write-Step "Installing from: $SourceDir"

# 2. Destination Directory (%LOCALAPPDATA%\Programs\MedTRx)
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\MedTRx"
Write-Step "Target destination: $InstallDir"

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# 3. Copy Application Files
Write-Step "Deploying application files..."

$FilesToCopy = @(
    "MedTRx.exe",
    "Microsoft.Web.WebView2.Core.dll",
    "Microsoft.Web.WebView2.WinForms.dll",
    "WebView2Loader.dll",
    "logo.ico",
    "ErrorPage.html",
    "MicrosoftEdgeWebview2Setup.exe"
)

foreach ($file in $FilesToCopy) {
    $src = Join-Path $SourceDir $file
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $InstallDir $file) -Force
    }
}

# Copy runtimes directory if present (SDK native loader)
$RuntimesSrc = Join-Path $SourceDir "runtimes"
if (Test-Path $RuntimesSrc) {
    Copy-Item $RuntimesSrc -Destination (Join-Path $InstallDir "runtimes") -Recurse -Force
}

# Copy bundled 100% offline Fixed Version runtime if present
$OfflineRuntimeSrc = Join-Path $SourceDir "runtime"
if (Test-Path $OfflineRuntimeSrc) {
    Write-Step "Deploying bundled 100% offline WebView2 runtime folder..."
    Copy-Item $OfflineRuntimeSrc -Destination (Join-Path $InstallDir "runtime") -Recurse -Force
    Write-Success "Offline runtime deployed to $InstallDir\runtime."
}

# Copy config.json only if destination does not already have a customized config
$DestConfig = Join-Path $InstallDir "config.json"
$SrcConfig = Join-Path $SourceDir "config.json"
if ((-not (Test-Path $DestConfig)) -and (Test-Path $SrcConfig)) {
    Copy-Item $SrcConfig -Destination $DestConfig -Force
    Write-Step "Copied default configuration."
} else {
    Write-Step "Preserving existing configuration at $DestConfig."
}

# Copy uninstaller scripts into install directory
$UninstallBat = Join-Path $ScriptDir "uninstall.bat"
$UninstallPs1 = Join-Path $ScriptDir "uninstall.ps1"
if (Test-Path $UninstallBat) { Copy-Item $UninstallBat -Destination (Join-Path $InstallDir "uninstall.bat") -Force }
if (Test-Path $UninstallPs1) { Copy-Item $UninstallPs1 -Destination (Join-Path $InstallDir "uninstall.ps1") -Force }

# 4. Check and Install Microsoft Edge WebView2 Runtime if missing
$HasBundledOffline = (Test-Path (Join-Path $InstallDir "runtime\msedgewebview2.exe")) -or (Test-Path (Join-Path $SourceDir "runtime\msedgewebview2.exe"))

if ($HasBundledOffline) {
    Write-Success "Bundled 100% offline WebView2 Runtime is detected. No system-wide installation required!"
} else {
    Write-Step "Checking Microsoft Edge WebView2 Runtime..."
    if (-not (Test-WebView2Installed)) {
        Write-WarnMsg "Microsoft Edge WebView2 Runtime was not detected on this system."
        Write-Step "Installing Microsoft Edge WebView2 Evergreen Runtime..."
    
    $BootstrapperPath = Join-Path $InstallDir "MicrosoftEdgeWebview2Setup.exe"
    if (-not (Test-Path $BootstrapperPath)) {
        $BootstrapperPath = Join-Path $SourceDir "MicrosoftEdgeWebview2Setup.exe"
    }
    if (-not (Test-Path $BootstrapperPath)) {
        $BootstrapperPath = Join-Path $env:TEMP "MicrosoftEdgeWebview2Setup.exe"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Write-Step "Downloading MicrosoftEdgeWebview2Setup.exe..."
        try {
            Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $BootstrapperPath -UseBasicParsing
        } catch {
            Write-WarnMsg "Failed to download WebView2 setup: $($_.Exception.Message)"
        }
    }

    if (Test-Path $BootstrapperPath) {
        Write-Step "Running WebView2 installer..."
        $installProc = Start-Process -FilePath $BootstrapperPath -ArgumentList "/silent /install" -Wait -PassThru
        if ($installProc.ExitCode -eq 0 -or (Test-WebView2Installed)) {
            Write-Success "Microsoft Edge WebView2 Runtime installed successfully!"
        } else {
            Write-WarnMsg "Silent install exited with code $($installProc.ExitCode). Launching interactive setup..."
            Start-Process -FilePath $BootstrapperPath -Wait
        }
    }
} else {
    Write-Success "Microsoft Edge WebView2 Runtime is already installed."
}
}

# 5. Create Shortcuts (Desktop & Start Menu)
Write-Step "Creating Desktop and Start Menu shortcuts..."

$WshShell = New-Object -ComObject WScript.Shell
$TargetExe = Join-Path $InstallDir "MedTRx.exe"
$IconPath = Join-Path $InstallDir "logo.ico"

# Helper for creating shortcut
function Create-AppShortcut([string]$shortcutPath) {
    $shortcut = $WshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $TargetExe
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.Description = "MedTRx Application"
    if (Test-Path $IconPath) {
        $shortcut.IconLocation = "$IconPath, 0"
    }
    $shortcut.Save()
}

# Desktop Shortcut
$DesktopDir = [Environment]::GetFolderPath("Desktop")
$DesktopShortcut = Join-Path $DesktopDir "MedTRx.lnk"
Create-AppShortcut $DesktopShortcut
Write-Success "Desktop shortcut created: $DesktopShortcut"

# Start Menu Shortcut
$StartMenuDir = [Environment]::GetFolderPath("Programs")
$StartMenuShortcut = Join-Path $StartMenuDir "MedTRx.lnk"
Create-AppShortcut $StartMenuShortcut
Write-Success "Start Menu shortcut created: $StartMenuShortcut"

# 5. Register in Windows Add/Remove Programs (HKCU)
Write-Step "Registering in Windows Installed Apps..."
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MedTRx"
try {
    if (-not (Test-Path $UninstallKey)) {
        New-Item -Path $UninstallKey -Force | Out-Null
    }
    Set-ItemProperty -Path $UninstallKey -Name "DisplayName" -Value "MedTRx Application"
    Set-ItemProperty -Path $UninstallKey -Name "DisplayIcon" -Value "$IconPath,0"
    Set-ItemProperty -Path $UninstallKey -Name "DisplayVersion" -Value "1.0.0"
    Set-ItemProperty -Path $UninstallKey -Name "Publisher" -Value "MedTRx Healthcare Systems"
    Set-ItemProperty -Path $UninstallKey -Name "InstallLocation" -Value $InstallDir
    Set-ItemProperty -Path $UninstallKey -Name "UninstallString" -Value "`"$InstallDir\uninstall.bat`""
    Set-ItemProperty -Path $UninstallKey -Name "NoModify" -Value 1 -Type DWord
    Set-ItemProperty -Path $UninstallKey -Name "NoRepair" -Value 1 -Type DWord
    Write-Success "Registered in Windows Settings / Installed Apps."
} catch {
    Write-WarnMsg "Could not write registry uninstall key (non-fatal)."
}

# 6. Taskbar Pinning Attempt
Write-Step "Attempting to pin MedTRx to Taskbar..."
$Pinned = $false

try {
    # Method A: Shell Application verb invocation
    $Shell = New-Object -ComObject Shell.Application
    $Folder = $Shell.NameSpace($InstallDir)
    $Item = $Folder.ParseName("MedTRx.exe")
    
    if ($Item) {
        $verbs = $Item.Verbs()
        foreach ($verb in $verbs) {
            $name = $verb.Name.Replace("&", "")
            if ($name -match "pin to taskbar" -or $name -match "taskbarpin") {
                $verb.DoIt()
                $Pinned = $true
                Write-Success "Pinned MedTRx to Taskbar successfully via shell verb!"
                break
            }
        }
    }
} catch { }

if (-not $Pinned) {
    # Windows 11 policy often disallows automated taskbar pinning verbs to prevent app spam.
    Write-WarnMsg "Notice: Modern Windows 10/11 taskbar security requires manual pinning."
    Write-Host "    -> Simply right-click 'MedTRx' on your Desktop or Start Menu and click 'Pin to taskbar'." -ForegroundColor White
    Write-Host "    -> MedTRx is configured with AppUserModelID so when opened, it pins seamlessly!" -ForegroundColor White
}

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  INSTALLATION COMPLETED SUCCESSFULLY!           " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host "You can now launch MedTRx from your Desktop or Start Menu."
