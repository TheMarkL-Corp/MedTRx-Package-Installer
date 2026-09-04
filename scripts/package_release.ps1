<#
.SYNOPSIS
    Packages the compiled dist/ directory into a standalone ZIP release package.
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0.0"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $ProjectDir "dist"
$ReleaseDir = Join-Path $ProjectDir "release"

# Read version from VERSION file if default or not supplied
$VersionFile = Join-Path $ProjectDir "VERSION"
if (Test-Path $VersionFile) {
    $fileVer = (Get-Content $VersionFile -Raw).Trim()
    if ($PSBoundParameters.ContainsKey('Version') -eq $false) {
        $Version = $fileVer
    }
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx Release Packager v$Version              " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Verify / Trigger Build to ensure binary has matching version
Write-Host "[*] Compiling MedTRx.exe with version $Version..." -ForegroundColor Yellow
& (Join-Path $ScriptDir "build.ps1")

# 2. Ensure release directory exists
if (-not (Test-Path $ReleaseDir)) {
    New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
}

# 3. Create Release Archive
$ZipName = "MedTRx-v$Version-Portable.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Write-Host "[*] Compressing $DistDir into $ZipPath..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($DistDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$ZipItem = Get-Item $ZipPath
$ZipSizeMB = [math]::Round($ZipItem.Length / 1MB, 2)
if ($ZipSizeMB -eq 0) {
    $ZipSizeKB = [math]::Round($ZipItem.Length / 1KB, 1)
    Write-Host "[+] Package created: $ZipName ($ZipSizeKB KB)" -ForegroundColor Green
} else {
    Write-Host "[+] Package created: $ZipName ($ZipSizeMB MB)" -ForegroundColor Green
}

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  RELEASE PACKAGE READY:                         " -ForegroundColor Green
Write-Host "  $ZipPath                                       " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
