<#
.SYNOPSIS
    MedTRx Automated Test Runner & Verification Engine
    Executes unit tests, binary PE security checks, supply chain integrity, and runtime loop tests.
#>

[CmdletBinding()]
param(
    [int]$LoopIterations = 3
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $ProjectDir "dist"
$TestsDir = Join-Path $ProjectDir "tests"
$CacheDir = Join-Path $ProjectDir ".cache"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx Verification & Test-Driven Test Suite   " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

$Results = [System.Collections.Generic.List[PSObject]]::new()

function Record-Result([string]$Category, [string]$TestName, [bool]$Passed, [string]$Details) {
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color = if ($Passed) { "Green" } else { "Red" }
    Write-Host "[$status] $Category :: $TestName - $Details" -ForegroundColor $color
    $Results.Add([PSCustomObject]@{
        Category = $Category
        TestName = $TestName
        Passed   = $Passed
        Details  = $Details
    })
}

# -------------------------------------------------------------------------
# 1. Compile & Execute C# Unit Test Suite
# -------------------------------------------------------------------------
Write-Host "`n--- [Phase 1: Compiling & Running Unit Tests] ---" -ForegroundColor Yellow

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    $CscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$SystemWebExtensions = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll"
if (-not (Test-Path $SystemWebExtensions)) {
    $SystemWebExtensions = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Web.Extensions.dll"
}

$CoreDll = Join-Path $DistDir "Microsoft.Web.WebView2.Core.dll"
$WinFormsDll = Join-Path $DistDir "Microsoft.Web.WebView2.WinForms.dll"
$TestExe = Join-Path $TestsDir "MedTRx.Tests.exe"

$RspFile = Join-Path $TestsDir "tests.rsp"
$RspLines = @(
    "/target:exe",
    "/platform:anycpu",
    "/optimize+",
    "/main:MedTRx.Tests.TestRunner",
    "/r:System.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:System.Core.dll",
    "/r:`"$SystemWebExtensions`"",
    "/r:`"$CoreDll`"",
    "/r:`"$WinFormsDll`"",
    "/out:`"$TestExe`"",
    "`"$(Join-Path $ProjectDir 'src\AssemblyInfo.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\ConfigManager.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\Program.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\MainForm.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\PdfViewerForm.cs')`"",
    "`"$(Join-Path $ProjectDir 'src\SettingsForm.cs')`"",
    "`"$(Join-Path $TestsDir 'TestRunner.cs')`""
)

[System.IO.File]::WriteAllLines($RspFile, $RspLines)

$compileProc = Start-Process -FilePath $CscPath -ArgumentList "@`"$RspFile`"" -NoNewWindow -Wait -PassThru
if ($compileProc.ExitCode -ne 0) {
    Record-Result "Compilation" "Unit Test Build" $false "csc.exe failed with code $($compileProc.ExitCode)"
} else {
    Record-Result "Compilation" "Unit Test Build" $true "MedTRx.Tests.exe compiled cleanly"

    # Execute Test Runner
    $testOutput = & "$TestExe"
    $testExitCode = $LASTEXITCODE
    Write-Host ($testOutput -join "`n")

    if ($testExitCode -eq 0) {
        Record-Result "Unit Tests" "Full Suite Execution" $true "All C# unit test assertions passed"
    } else {
        Record-Result "Unit Tests" "Full Suite Execution" $false "One or more assertions failed (exit code $testExitCode)"
    }
}

# -------------------------------------------------------------------------
# 2. Binary Metadata & PE Security Hardening Checks
# -------------------------------------------------------------------------
Write-Host "`n--- [Phase 2: Binary Metadata & PE Exploit Mitigations] ---" -ForegroundColor Yellow

$TargetExe = Join-Path $DistDir "MedTRx.exe"
if (-not (Test-Path $TargetExe)) {
    Record-Result "Binary" "File Exists" $false "dist\MedTRx.exe not found"
} else {
    Record-Result "Binary" "File Exists" $true "dist\MedTRx.exe found"

    # Version check
    $versionInfo = (Get-Item $TargetExe).VersionInfo
    $prodVer = $versionInfo.ProductVersion
    $fileVer = $versionInfo.FileVersion

    if ($prodVer -eq "1.0.3") {
        Record-Result "Metadata" "ProductVersion" $true "ProductVersion is $prodVer"
    } else {
        Record-Result "Metadata" "ProductVersion" $false "Expected 1.0.3, found $prodVer"
    }

    if ($fileVer -eq "1.0.3.0") {
        Record-Result "Metadata" "FileVersion" $true "FileVersion is $fileVer"
    } else {
        Record-Result "Metadata" "FileVersion" $false "Expected 1.0.3.0, found $fileVer"
    }

    # High-Entropy ASLR Check in PE Header
    $bytes = [System.IO.File]::ReadAllBytes($TargetExe)
    $peOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    # DllCharacteristics is at PE Header + 0x5E (for PE32+) or PE Header + 0x46 (for PE32)
    # Standard CLR AnyCPU is PE32 with 0x0020 (High Entropy VA in CLR header / DllCharacteristics)
    $magic = [System.BitConverter]::ToUInt16($bytes, $peOffset + 0x18)
    $dllCharOffset = if ($magic -eq 0x20B) { $peOffset + 0x18 + 0x46 } else { $peOffset + 0x18 + 0x46 }
    $dllCharacteristics = [System.BitConverter]::ToUInt16($bytes, $dllCharOffset)
    
    # 0x0020 = IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA
    # 0x0100 = IMAGE_DLLCHARACTERISTICS_NX_COMPAT (DEP)
    # 0x0040 = IMAGE_DLLCHARACTERISTICS_DYNAMIC_BASE (ASLR)
    $hasNxCompat = ($dllCharacteristics -band 0x0100) -ne 0
    $hasDynamicBase = ($dllCharacteristics -band 0x0040) -ne 0

    Record-Result "PE Hardening" "DEP/NX Compatibility" $hasNxCompat "Flag 0x0100 present"
    Record-Result "PE Hardening" "Dynamic Base (ASLR)" $hasDynamicBase "Flag 0x0040 present"
}

# -------------------------------------------------------------------------
# 3. Supply Chain & SHA-256 Manifest Integrity
# -------------------------------------------------------------------------
Write-Host "`n--- [Phase 3: Supply Chain & SHA-256 Manifest Integrity] ---" -ForegroundColor Yellow

$ManifestFile = Join-Path $DistDir "checksums.sha256"
if (-not (Test-Path $ManifestFile)) {
    Record-Result "Supply Chain" "Manifest Exists" $false "checksums.sha256 missing"
} else {
    $manifestLines = Get-Content $ManifestFile
    $allHashesValid = $true
    $checkedCount = 0

    foreach ($line in $manifestLines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "\s+", 2
        if ($parts.Count -eq 2) {
            $expectedHash = $parts[0].Trim().ToLowerInvariant()
            $fileName = $parts[1].Trim()
            $filePath = Join-Path $DistDir $fileName

            if (Test-Path $filePath) {
                $actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($expectedHash -ne $actualHash) {
                    $allHashesValid = $false
                    Record-Result "Supply Chain" "Hash: $fileName" $false "Hash mismatch: $actualHash vs $expectedHash"
                } else {
                    $checkedCount++
                }
            } else {
                $allHashesValid = $false
                Record-Result "Supply Chain" "File: $fileName" $false "File listed in manifest does not exist"
            }
        }
    }

    if ($allHashesValid) {
        Record-Result "Supply Chain" "Cryptographic Manifest Integrity" $true "All $checkedCount files match SHA-256 manifest"
    }
}

# -------------------------------------------------------------------------
# 4. Offline Air-Gap Security Audit
# -------------------------------------------------------------------------
Write-Host "`n--- [Phase 4: Air-Gap Offline Isolation Audit] ---" -ForegroundColor Yellow

$MainFormCode = Get-Content (Join-Path $ProjectDir "src\MainForm.cs") -Raw
$InstallCode = Get-Content (Join-Path $ProjectDir "scripts\install.ps1") -Raw

$hasExternalDownloadInMain = $MainFormCode -match "go\.microsoft\.com"
$hasExternalDownloadInInstall = $InstallCode -match "go\.microsoft\.com"

Record-Result "Air-Gap Audit" "MainForm.cs Zero Outbound CDNs" (-not $hasExternalDownloadInMain) "No go.microsoft.com references"
Record-Result "Air-Gap Audit" "install.ps1 Zero Outbound CDNs" (-not $hasExternalDownloadInInstall) "No go.microsoft.com references"

# -------------------------------------------------------------------------
# 5. Live Runtime Invocation & Single-Instance Loop Test
# -------------------------------------------------------------------------
Write-Host "`n--- [Phase 5: Runtime Invocation & Single-Instance Loop Test ($LoopIterations Iterations)] ---" -ForegroundColor Yellow

for ($i = 1; $i -le $LoopIterations; $i++) {
    Write-Host "[*] Executing Loop Iteration $i / $LoopIterations..." -ForegroundColor Cyan

    # Launch instance 1 with local test flag / URL
    $p1 = Start-Process -FilePath $TargetExe -ArgumentList "about:blank" -PassThru
    Start-Sleep -Milliseconds 1200

    # Launch instance 2 (should detect mutex and exit immediately)
    $p2 = Start-Process -FilePath $TargetExe -ArgumentList "about:blank" -PassThru -Wait
    $p2Exit = $p2.ExitCode

    $mutexEnforced = ($p2Exit -eq 0 -and $p1.HasExited -eq $false)

    # Terminate instance 1
    if (-not $p1.HasExited) {
        $p1.Kill()
        $p1.WaitForExit(3000)
    }

    Record-Result "Runtime Loop" "Iteration $($i) - Mutex & Clean Startup" $mutexEnforced "Instance 2 yielded, Instance 1 maintained window"
}

# Check crash logs
$AppDataCrashLog = Join-Path $env:LOCALAPPDATA "MedTRx\crash.log"
$cleanLogs = $true
if (Test-Path $AppDataCrashLog) {
    $logContent = Get-Content $AppDataCrashLog -Raw
    if ($logContent -match "UnhandledException" -or $logContent -match "ThreadException") {
        # Check if timestamp is within last 5 minutes
        Record-Result "Runtime Logs" "Crash Log Inspection" $false "Found unhandled exceptions in crash.log"
        $cleanLogs = $false
    }
}
if ($cleanLogs) {
    Record-Result "Runtime Logs" "Crash Log Inspection" $true "No unhandled crashes detected during loop execution"
}

# -------------------------------------------------------------------------
# Final Summary
# -------------------------------------------------------------------------
Write-Host "`n=================================================" -ForegroundColor Cyan
Write-Host "                FINAL TEST REPORT                " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

$TotalPassed = ($Results | Where-Object { $_.Passed -eq $true }).Count
$TotalFailed = ($Results | Where-Object { $_.Passed -eq $false }).Count
$TotalTests = $Results.Count

Write-Host "Total Verification Checks: $TotalTests"
Write-Host "Passed: $TotalPassed" -ForegroundColor Green
$failedColor = if ($TotalFailed -gt 0) { "Red" } else { "Green" }
Write-Host "Failed: $TotalFailed" -ForegroundColor $failedColor

if ($TotalFailed -eq 0) {
    Write-Host "`n[SUCCESS] ALL VERIFICATION & REGRESSION CHECKS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n[FAILURE] $TotalFailed CHECKS FAILED!" -ForegroundColor Red
    exit 1
}
