<#
.SYNOPSIS
    Erstellt ein selbstsigniertes Zertifikat fuer lokales Code-Signing von SpeedScanManager.
.DESCRIPTION
    Dieses Skript erstellt ein self-signed Certificate (.pfx) und speichert es im installer/cert-Ordner.
    Das Passwort fuer die PFX-Datei wird abgefragt (kein Standardpasswort).
    Das Zertifikat wird im Store "Vertrauenswuerdige Herausgeber" (CurrentUser) importiert.
    Es wird NICHT automatisch als Stammzertifizierungsstelle eingetragen - die noetigen
    Schritte werden am Ende ausgegeben und muessen bewusst manuell ausgefuehrt werden.
#>

$ErrorActionPreference = "Stop"

$certDir = Join-Path $PSScriptRoot "cert"
if (-not (Test-Path $certDir)) { New-Item -ItemType Directory -Force $certDir | Out-Null }

$securePwd = Read-Host -AsSecureString "Passwort fuer die PFX-Datei"
$securePwd2 = Read-Host -AsSecureString "Passwort wiederholen"
$plain1 = [System.Net.NetworkCredential]::new("", $securePwd).Password
$plain2 = [System.Net.NetworkCredential]::new("", $securePwd2).Password
if ([string]::IsNullOrEmpty($plain1)) { throw "Ein leeres Passwort ist nicht erlaubt." }
if ($plain1 -ne $plain2) { throw "Die Passwoerter stimmen nicht ueberein." }
$plain1 = $null; $plain2 = $null

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

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd | Out-Null

Write-Host "Zertifikat erstellt: $pfxPath (Thumbprint: $thumbprint)" -ForegroundColor Green

$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "CurrentUser")
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]"ReadWrite")
$store.Add($cert)
$store.Close()

Write-Host "Im 'Vertrauenswuerdige Herausgeber'-Store (CurrentUser) importiert" -ForegroundColor Green

Write-Host ""
Write-Host "Hinweis: Das Zertifikat wurde NICHT als Stammzertifizierungsstelle eingetragen." -ForegroundColor Yellow
Write-Host "Ein selbstsigniertes Stammzertifikat vertraut jedem damit signierten Code - nur auf"
Write-Host "eigenen Testrechnern verwenden. Falls gewuenscht, manuell eintragen mit:"
Write-Host "  `$c = Get-Item Cert:\CurrentUser\My\$thumbprint"
Write-Host "  `$s = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root','CurrentUser')"
Write-Host "  `$s.Open('ReadWrite'); `$s.Add(`$c); `$s.Close()"
Write-Host ""
Write-Host "Signieren (Beispiel):"
Write-Host "  signtool sign /f `"$pfxPath`" /p <Passwort> /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 <Datei.exe>"
