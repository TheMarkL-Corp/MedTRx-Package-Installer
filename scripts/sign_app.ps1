<#
.SYNOPSIS
    Signs MedTRx binaries with Authenticode digital signature.
    Generates an enterprise/self-signed Code Signing Certificate if none exists,
    exports public MedTRx_Publisher.cer, imports it into Trusted Publishers,
    and timestamps the signature via SHA-256.
#>

[CmdletBinding()]
param(
    [string]$Subject = "CN=MedTRx Healthcare Systems, O=MedTRx Healthcare Systems, OU=AMiS Cart Deployment, C=US",
    [string]$FriendlyName = "MedTRx Code Signing Certificate",
    [string]$TimestampServer = "http://timestamp.digicert.com",
    [switch]$SkipTimestamp
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Split-Path -Parent $ScriptDir
$DistDir = Join-Path $ProjectDir "dist"
$AssetsDir = Join-Path $ProjectDir "assets"
$CerPath = Join-Path $DistDir "MedTRx_Publisher.cer"
$CerAssetPath = Join-Path $AssetsDir "MedTRx_Publisher.cer"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  MedTRx Authenticode Code-Signing Engine        " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. Locate or Generate Code Signing Certificate
Write-Host "[*] Searching for existing MedTRx code-signing certificate..." -ForegroundColor Yellow

$cert = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -match "MedTRx Healthcare Systems" -and $_.NotAfter -gt (Get-Date) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "[*] No existing certificate found. Generating 10-year enterprise code-signing certificate..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -FriendlyName $FriendlyName `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(10)
    
    Write-Host "[+] Generated certificate with Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    Write-Host "[+] Found active code-signing certificate: $($cert.Thumbprint) (Expires: $($cert.NotAfter.ToShortDateString()))" -ForegroundColor Green
}

# 2. Export Public Certificate (.cer)
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir -Force | Out-Null }
if (-not (Test-Path $AssetsDir)) { New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null }

Write-Host "[*] Exporting public certificate to $CerPath..." -ForegroundColor Yellow
Export-Certificate -Cert $cert -FilePath $CerPath -Force | Out-Null
Copy-Item $CerPath -Destination $CerAssetPath -Force
Write-Host "[+] Public certificate exported: $CerPath" -ForegroundColor Green

# 3. Import Public Certificate to Local Trusted Publisher Store
Write-Host "[*] Registering certificate into Trusted Publishers store on this machine..." -ForegroundColor Yellow
try {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isAdmin) {
        & certutil.exe -addstore -f "TrustedPublisher" "$CerPath" | Out-Null
        & certutil.exe -addstore -f "Root" "$CerPath" | Out-Null
        Write-Host "[+] Certificate registered in LocalMachine Trusted Publishers & Root." -ForegroundColor Green
    } else {
        & certutil.exe -user -addstore -f "TrustedPublisher" "$CerPath" | Out-Null
        Write-Host "[+] Certificate registered in CurrentUser Trusted Publishers." -ForegroundColor Green
    }
} catch {
    Write-Host "[!] Note: Could not auto-import certificate: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 4. Sign Target Binaries
$TargetFiles = @(
    (Join-Path $DistDir "MedTRx.exe"),
    (Join-Path $DistDir "WebView2Loader.dll"),
    (Join-Path $DistDir "runtimes\win-x64\native\WebView2Loader.dll")
)

$OfflineDist = Join-Path $ProjectDir "dist-offline"
if (Test-Path $OfflineDist) {
    $TargetFiles += (Join-Path $OfflineDist "MedTRx.exe")
    $TargetFiles += (Join-Path $OfflineDist "WebView2Loader.dll")
    $TargetFiles += (Join-Path $OfflineDist "runtimes\win-x64\native\WebView2Loader.dll")
    Copy-Item $CerPath -Destination (Join-Path $OfflineDist "MedTRx_Publisher.cer") -Force
}

foreach ($target in $TargetFiles) {
    if (Test-Path $target) {
        Write-Host "[*] Signing $(Split-Path -Leaf $target)..." -ForegroundColor Yellow
        $signed = $false
        
        # Attempt signing with timestamp server
        if (-not $SkipTimestamp) {
            try {
                $sig = Set-AuthenticodeSignature -FilePath $target `
                    -Certificate $cert `
                    -TimestampServer $TimestampServer `
                    -HashAlgorithm SHA256 `
                    -ErrorAction Stop
                
                if ($sig.Status -eq "Valid") {
                    $signed = $true
                    Write-Host "[+] Signed with timestamp: $target" -ForegroundColor Green
                }
            } catch {
                Write-Host "[!] Timestamp server unreachable. Falling back to local signing..." -ForegroundColor Yellow
            }
        }
        
        # Fallback without timestamp if needed
        if (-not $signed) {
            $sig = Set-AuthenticodeSignature -FilePath $target `
                -Certificate $cert `
                -HashAlgorithm SHA256
            Write-Host "[+] Signed (local): $target [Status: $($sig.Status)]" -ForegroundColor Green
        }
    }
}

# 5. Recompute Checksum Manifest
Write-Host "[*] Updating SHA-256 checksum manifest..." -ForegroundColor Yellow
$ChecksumFile = Join-Path $DistDir "checksums.sha256"
$ChecksumLines = @()
Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -ne "checksums.sha256" } | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $ChecksumLines += "$hash  $($_.Name)"
}
[System.IO.File]::WriteAllLines($ChecksumFile, $ChecksumLines)
Write-Host "[+] Checksum manifest updated: $ChecksumFile" -ForegroundColor Green

# 6. Verification Summary
Write-Host "=================================================" -ForegroundColor Green
Write-Host "  SIGNING & VERIFICATION SUMMARY                 " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
$exeCheck = Join-Path $DistDir "MedTRx.exe"
if (Test-Path $exeCheck) {
    $finalSig = Get-AuthenticodeSignature $exeCheck
    Write-Host "File:       $exeCheck"
    Write-Host "Status:     $($finalSig.Status)" -ForegroundColor Green
    Write-Host "Signer:     $($finalSig.SignerCertificate.Subject)" -ForegroundColor Cyan
    Write-Host "Thumbprint: $($finalSig.SignerCertificate.Thumbprint)"
    Write-Host "Timestamp:  $($finalSig.TimeStamperCertificate.Subject)"
}
Write-Host "=================================================" -ForegroundColor Green
