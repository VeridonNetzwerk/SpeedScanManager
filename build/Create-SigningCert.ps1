<#
.SYNOPSIS
    Erstellt ein selbstsigniertes Zertifikat fuer lokale Code-Signing von SpeedScanManager.
.DESCRIPTION
    Dieses Skript erstellt ein self-signed Certificate (.pfx) und speichert es im build/cert-Ordner.
    Ausserdem wird es im Trusted Publisher Store importiert, damit Windows die Warnung unterdrueckt.
.PARAMETER CertPassword
    Passwort fuer die PFX-Datei. Standard: "SpeedScan2026".
#>
param(
    [string]$CertPassword = "SpeedScan2026"
)

$certDir = Join-Path $PSScriptRoot "cert"
if (-not (Test-Path $certDir)) { New-Item -ItemType Directory -Force $certDir | Out-Null }

Write-Host "Erstelle selbstsigniertes Zertifikat..." -ForegroundColor Cyan

$cert = New-SelfSignedCertificate `
    -Type CodeSigning `
    -Subject "CN=VeridonNetzwerk" `
    -KeyUsage DigitalSignature `
    -HashAlgorithm SHA256 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

$pfxPath = Join-Path $certDir "SpeedScan.pfx"
$thumbprint = $cert.Thumbprint

$securePwd = ConvertTo-SecureString -String $CertPassword -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd | Out-Null

Write-Host "Zertifikat erstellt: $pfxPath (Thumbprint: $thumbprint)" -ForegroundColor Green

$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "CurrentUser")
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]"ReadWrite")
$store.Add($cert)
$store.Close()

Write-Host "Im 'Vertrauenswuerdige Herausgeber'-Store (CurrentUser) importiert" -ForegroundColor Green

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
$rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]"ReadWrite")
$rootStore.Add($cert)
$rootStore.Close()

Write-Host "Im 'Vertrauenswuerdige Stammzertifizierungsstellen'-Store (CurrentUser) importiert" -ForegroundColor Green
Write-Host "Fertig. Die .csproj signiert zukuenftig automatisch bei dotnet publish." -ForegroundColor Yellow
