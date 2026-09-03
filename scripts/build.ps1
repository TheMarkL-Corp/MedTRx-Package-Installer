<#
.SYNOPSIS
    Builds the MedTRx Native Windows App into a standalone portable distribution.
    Uses Windows built-in csc.exe (no Visual Studio or .NET SDK installation required).
#>

[CmdletBinding()]
param(
    [string]$PackageVersion = "1.0.2903.40"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $ProjectDir "dist"
$CacheDir = Join-Path $ProjectDir ".cache"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx Native App - 1-Click Build Engine       " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Locate C# Compiler (csc.exe)
$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    $CscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $CscPath)) {
    Write-Error "Could not locate .NET Framework C# compiler (csc.exe)."
    exit 1
}
Write-Host "[+] Found C# Compiler: $CscPath" -ForegroundColor Green

# 2. Prepare Directories
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir -Force | Out-Null }
if (-not (Test-Path $CacheDir)) { New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null }

# 3. Ensure Microsoft.Web.WebView2 Package is Downloaded & Extracted
$NugetPackagePath = Join-Path $CacheDir "Microsoft.Web.WebView2.$PackageVersion.nupkg"
$NugetExtractDir = Join-Path $CacheDir "webview2_$PackageVersion"

if (-not (Test-Path $NugetPackagePath)) {
    Write-Host "[*] Downloading Microsoft.Web.WebView2 ($PackageVersion)..." -ForegroundColor Yellow
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $DownloadUrl = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$PackageVersion"
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $NugetPackagePath -UseBasicParsing
    Write-Host "[+] Download complete." -ForegroundColor Green
}

if (-not (Test-Path $NugetExtractDir)) {
    Write-Host "[*] Extracting WebView2 assemblies..." -ForegroundColor Yellow
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($NugetPackagePath, $NugetExtractDir)
}

# 4. Copy Required Libraries to dist/
Write-Host "[*] Copying runtime dependencies..." -ForegroundColor Yellow

# Locate Core and WinForms dlls
$CoreDll = Get-ChildItem -Path (Join-Path $NugetExtractDir "lib") -Recurse -Filter "Microsoft.Web.WebView2.Core.dll" | Select-Object -First 1
$WinFormsDll = Get-ChildItem -Path (Join-Path $NugetExtractDir "lib") -Recurse -Filter "Microsoft.Web.WebView2.WinForms.dll" | Select-Object -First 1

if (-not $CoreDll -or -not $WinFormsDll) {
    Write-Error "Could not find WebView2 managed assemblies in extracted package."
    exit 1
}

Copy-Item $CoreDll.FullName -Destination (Join-Path $DistDir "Microsoft.Web.WebView2.Core.dll") -Force
Copy-Item $WinFormsDll.FullName -Destination (Join-Path $DistDir "Microsoft.Web.WebView2.WinForms.dll") -Force

# Locate native WebView2Loader.dll (prefer x64, fallback x86)
$Loader64 = Join-Path $NugetExtractDir "runtimes\win-x64\native\WebView2Loader.dll"
$Loader86 = Join-Path $NugetExtractDir "runtimes\win-x86\native\WebView2Loader.dll"

$RuntimesDir64 = Join-Path $DistDir "runtimes\win-x64\native"
$RuntimesDir86 = Join-Path $DistDir "runtimes\win-x86\native"
New-Item -ItemType Directory -Path $RuntimesDir64 -Force | Out-Null
New-Item -ItemType Directory -Path $RuntimesDir86 -Force | Out-Null

if (Test-Path $Loader64) {
    Copy-Item $Loader64 -Destination $RuntimesDir64 -Force
    Copy-Item $Loader64 -Destination (Join-Path $DistDir "WebView2Loader.dll") -Force
}
if (Test-Path $Loader86) {
    Copy-Item $Loader86 -Destination $RuntimesDir86 -Force
}

# 5. Copy Static Assets & Scripts
$IconSource = Join-Path $ProjectDir "assets\logo.ico"
if (Test-Path $IconSource) {
    Copy-Item $IconSource -Destination (Join-Path $DistDir "logo.ico") -Force
}

$ConfigDefault = Join-Path $ProjectDir "config.json"
$ConfigDist = Join-Path $DistDir "config.json"
if (-not (Test-Path $ConfigDist) -and (Test-Path $ConfigDefault)) {
    Copy-Item $ConfigDefault -Destination $ConfigDist -Force
}

$ErrorHtmlSource = Join-Path $ProjectDir "src\ErrorPage.html"
if (Test-Path $ErrorHtmlSource) {
    Copy-Item $ErrorHtmlSource -Destination (Join-Path $DistDir "ErrorPage.html") -Force
}

Copy-Item (Join-Path $ScriptDir "install.bat") -Destination (Join-Path $DistDir "install.bat") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $ScriptDir "install.ps1") -Destination (Join-Path $DistDir "install.ps1") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $ScriptDir "uninstall.bat") -Destination (Join-Path $DistDir "uninstall.bat") -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $ScriptDir "uninstall.ps1") -Destination (Join-Path $DistDir "uninstall.ps1") -Force -ErrorAction SilentlyContinue

# 6. Build via Response File (.rsp)
Write-Host "[*] Generating compiler response file..." -ForegroundColor Yellow

$OutputExe = Join-Path $DistDir "MedTRx.exe"
$SystemWebExtensions = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll"
if (-not (Test-Path $SystemWebExtensions)) {
    $SystemWebExtensions = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Web.Extensions.dll"
}

$RspFile = Join-Path $CacheDir "build.rsp"
$RspLines = @(
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/win32icon:`"$IconSource`"",
    "/r:System.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:System.Core.dll",
    "/r:`"$SystemWebExtensions`"",
    "/r:`"$(Join-Path $DistDir 'Microsoft.Web.WebView2.Core.dll')`"",
    "/r:`"$(Join-Path $DistDir 'Microsoft.Web.WebView2.WinForms.dll')`"",
    "/out:`"$OutputExe`"",
    "`"$(Join-Path $ProjectDir 'src\Program.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\MainForm.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\SettingsForm.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\ConfigManager.cs')`""
)

[System.IO.File]::WriteAllLines($RspFile, $RspLines)

Write-Host "[*] Compiling MedTRx.exe..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $CscPath -ArgumentList "@`"$RspFile`"" -NoNewWindow -Wait -PassThru

if ($proc.ExitCode -ne 0) {
    Write-Error "Compilation failed with exit code $($proc.ExitCode)."
    exit $proc.ExitCode
}

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  BUILD SUCCESSFUL!                              " -ForegroundColor Green
Write-Host "  Binary: $OutputExe                             " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
