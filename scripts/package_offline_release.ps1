<#
.SYNOPSIS
    Downloads Microsoft Edge WebView2 Fixed Version Runtime and packages
    a 100% OFFLINE, self-contained MedTRx release bundle.
    Zero installation, zero admin rights, and zero internet required on target machines.
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0.1",
    [string]$Architecture = "x64"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $ProjectDir "dist"
$CacheDir = Join-Path $ProjectDir ".cache"
$ReleaseDir = Join-Path $ProjectDir "release"
$OfflineDistDir = Join-Path $ProjectDir "dist-offline"

# Read version from VERSION file if default or not supplied
$VersionFile = Join-Path $ProjectDir "VERSION"
if (Test-Path $VersionFile) {
    $fileVer = (Get-Content $VersionFile -Raw).Trim()
    if ($PSBoundParameters.ContainsKey('Version') -eq $false) {
        $Version = $fileVer
    }
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx 100% Offline Bundle Packager v$Version  " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Compile MedTRx with accurate stamped version
Write-Host "[*] Compiling MedTRx with version $Version..." -ForegroundColor Yellow
& (Join-Path $ScriptDir "build.ps1")

if (-not (Test-Path $CacheDir)) { New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null }
if (-not (Test-Path $ReleaseDir)) { New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null }

# 2. Query Microsoft API for Fixed Version x64 CAB URL
$CabUrl = $null
$FixedVer = "latest"

try {
    Write-Host "[*] Querying Microsoft API for Fixed Version Runtime..." -ForegroundColor Yellow
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $apiResp = Invoke-RestMethod -Uri "https://explore.microsoft.com/microsoft-edge/api/webview2" -TimeoutSec 15
    
    foreach ($release in $apiResp) {
        foreach ($build in $release.builds) {
            if ($build.architecture -eq $Architecture -and $build.url -match "\.cab$") {
                $CabUrl = $build.url
                $FixedVer = $release.version
                break
            }
        }
        if ($CabUrl) { break }
    }
} catch {
    Write-Host "[!] API query failed. Using verified fallback CDN URL." -ForegroundColor Yellow
}

# Fallback verified URL if API was unreachable
if (-not $CabUrl) {
    $CabUrl = "https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/0a4a34d9-ccaa-4cef-98b4-58cb313fbfeb/Microsoft.WebView2.FixedVersionRuntime.152.0.4191.62.x64.cab"
    $FixedVer = "152.0.4191.62"
}

$CabFileName = "Microsoft.WebView2.FixedVersionRuntime.$FixedVer.$Architecture.cab"
$CabFilePath = Join-Path $CacheDir $CabFileName

# 3. Download CAB file if not already cached
$ProgressPreference = 'SilentlyContinue'
$CabFullyDownloaded = (Test-Path $CabFilePath) -and ((Get-Item $CabFilePath).Length -gt 300MB)

if (-not $CabFullyDownloaded) {
    Write-Host "[*] Downloading Fixed Version Runtime ($FixedVer $Architecture)..." -ForegroundColor Yellow
    Write-Host "    Source: $CabUrl" -ForegroundColor Gray
    
    if (Get-Command "curl.exe" -ErrorAction SilentlyContinue) {
        & curl.exe -L -C - "$CabUrl" -o "$CabFilePath" --retry 3
    } else {
        $wc = New-Object System.Net.WebClient
        $wc.DownloadFile($CabUrl, $CabFilePath)
    }
    Write-Host "[+] Download complete." -ForegroundColor Green
} else {
    Write-Host "[+] Using cached runtime CAB: $CabFileName" -ForegroundColor Green
}

# 4. Extract CAB into .cache/fixed_extracted
$ExtractedDir = Join-Path $CacheDir "fixed_extracted_$FixedVer"
if (-not (Test-Path $ExtractedDir)) {
    Write-Host "[*] Extracting Fixed Version Runtime via expand.exe..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $ExtractedDir -Force | Out-Null
    $expandProc = Start-Process -FilePath "expand.exe" -ArgumentList "-F:* `"$CabFilePath`" `"$ExtractedDir`"" -NoNewWindow -Wait -PassThru
    if ($expandProc.ExitCode -ne 0) {
        Write-Error "expand.exe failed with code $($expandProc.ExitCode)."
        exit 1
    }
    Write-Host "[+] Extraction complete." -ForegroundColor Green
} else {
    Write-Host "[+] Using already extracted runtime from cache." -ForegroundColor Green
}

# Locate the folder containing msedgewebview2.exe
$RuntimeBinariesDir = $null
if (Test-Path (Join-Path $ExtractedDir "msedgewebview2.exe")) {
    $RuntimeBinariesDir = $ExtractedDir
} else {
    $sub = Get-ChildItem -Path $ExtractedDir -Directory | Where-Object { Test-Path (Join-Path $_.FullName "msedgewebview2.exe") } | Select-Object -First 1
    if ($sub) {
        $RuntimeBinariesDir = $sub.FullName
    }
}

if (-not $RuntimeBinariesDir) {
    Write-Error "Could not locate msedgewebview2.exe in extracted directory."
    exit 1
}

# 5. Assemble dist-offline
Write-Host "[*] Assembling 100% offline distribution folder..." -ForegroundColor Yellow
if (Test-Path $OfflineDistDir) {
    Remove-Item $OfflineDistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OfflineDistDir -Force | Out-Null

# Copy application binaries
$AppFiles = @(
    "MedTRx.exe",
    "Microsoft.Web.WebView2.Core.dll",
    "Microsoft.Web.WebView2.WinForms.dll",
    "WebView2Loader.dll",
    "logo.ico",
    "config.json",
    "ErrorPage.html",
    "install.bat",
    "install.ps1",
    "uninstall.bat",
    "uninstall.ps1"
)

foreach ($file in $AppFiles) {
    $src = Join-Path $DistDir $file
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $OfflineDistDir $file) -Force
    }
}

# Copy native runtimes folder
if (Test-Path (Join-Path $DistDir "runtimes")) {
    Copy-Item (Join-Path $DistDir "runtimes") -Destination (Join-Path $OfflineDistDir "runtimes") -Recurse -Force
}

# Copy Fixed Version runtime to dist-offline/runtime
$DestRuntime = Join-Path $OfflineDistDir "runtime"
Write-Host "[*] Copying Fixed Version Runtime to dist-offline\runtime..." -ForegroundColor Yellow
Copy-Item $RuntimeBinariesDir -Destination $DestRuntime -Recurse -Force
Write-Host "[+] Fixed Version Runtime bundled successfully." -ForegroundColor Green

# 6. Create Offline Release ZIP
$ZipName = "MedTRx-v$Version-100Percent-Offline-Bundle.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Write-Host "[*] Compressing $OfflineDistDir into $ZipPath..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($OfflineDistDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$ZipItem = Get-Item $ZipPath
$ZipSizeMB = [math]::Round($ZipItem.Length / 1MB, 1)

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  100% OFFLINE BUNDLE CREATED SUCCESSFULLY!      " -ForegroundColor Green
Write-Host "  Package: $ZipName ($ZipSizeMB MB)              " -ForegroundColor Green
Write-Host "  Location: $ZipPath                             " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
