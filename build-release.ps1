<#
.SYNOPSIS
    Builds SpeedScan Manager as a portable ZIP and as a self-contained installer.

.DESCRIPTION
    1. Publishes SpeedScanManager.csproj (self-contained, win-x86) into ./dist.
    2. Zips ./dist as the portable release. The same zip is the installer's app payload.
    3. Publishes installer/SpeedScanInstaller.csproj WITHOUT payload as a single-file exe:
       the uninstaller, which the setup installs as uninstall.exe. It is built in its own
       obj/bin folders so it can never pick up a payload from an earlier incremental build.
    4. Publishes installer/SpeedScanInstaller.csproj as a single-file exe with that zip
       (-p:AppPayloadZip=...) and the uninstaller (-p:UninstallerExe=...) embedded, so the
       setup is one self-contained file and uninstall.exe doesn't carry the app payload.
    5. Both artifacts end up in ./release/vX.X.X/:
         SpeedScanManager-X.X.X-portable-win-x86.zip
         SpeedScanManager-X.X.X-Setup-win-x86.exe

    App and installer get the same version (-p:Version=...).

.PARAMETER Version
    Optional override for the version. If omitted, it is read from SpeedScanManager.csproj
    (<Version> property).
#>
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Get-ProjectVersion {
    $csproj = Join-Path $root "SpeedScanManager.csproj"
    [xml]$xml = Get-Content $csproj
    $ver = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $ver) { throw "No <Version> found in $csproj." }
    return $ver.Trim()
}

function Invoke-Checked {
    param([string]$What, [scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)." }
}

if (-not $Version) {
    $Version = Get-ProjectVersion
}

Write-Host "=== SpeedScan Manager Release Build v$Version ===" -ForegroundColor Cyan

$distDir       = Join-Path $root "dist"
$distInstDir   = Join-Path $root "dist-installer"
$distUninstDir = Join-Path $distInstDir "uninstaller"
$releaseDir    = Join-Path $root "release\v$Version"

# --- Clean previous outputs ---
foreach ($dir in @($distDir, $distInstDir)) {
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
}
# Try to clean release dir, but don't fail if admin-created folders resist
if (Test-Path $releaseDir) {
    try { Remove-Item -Recurse -Force $releaseDir -ErrorAction Stop }
    catch {
        Write-Host "Warning: Could not fully clean $releaseDir (admin-created?). Using timestamped folder." -ForegroundColor DarkYellow
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $releaseDir = Join-Path $root "release\v$Version-$stamp"
    }
}
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

$portableZip   = Join-Path $releaseDir "SpeedScanManager-$Version-portable-win-x86.zip"
$installerExe  = Join-Path $releaseDir "SpeedScanManager-$Version-Setup-win-x86.exe"

# Common publish properties. %3B is the MSBuild escape for ';' (a literal ';' would split the property).
$commonProps = @(
    "-p:Version=$Version",
    "-p:SatelliteResourceLanguages=de%3Ben"
)

# --- 1) Publish app ---
Write-Host "`n[1/4] Publishing app..." -ForegroundColor Yellow
Invoke-Checked "App publish" {
    dotnet publish (Join-Path $root "SpeedScanManager.csproj") `
        -c Release -r win-x86 --self-contained true `
        @commonProps `
        -o $distDir
}
if (-not (Test-Path (Join-Path $distDir "SpeedScanManager.exe"))) {
    throw "SpeedScanManager.exe missing in $distDir."
}

# --- 2) Zip app (portable release + installer payload) ---
Write-Host "[2/4] Zipping app -> $portableZip" -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $distDir, $portableZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$installerProject = Join-Path $root "installer\SpeedScanInstaller.csproj"
$installerProps = @(
    "-c", "Release", "-r", "win-x86", "--self-contained", "true",
    "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true"
)

# --- 3) Publish uninstaller (installer without payload) ---
# Own obj/bin folders (relative to the project): otherwise the incremental build of a
# previous full installer could be reused and the uninstaller would contain the payload.
Write-Host "`n[3/4] Publishing uninstaller (no payload)..." -ForegroundColor Yellow
Invoke-Checked "Uninstaller publish" {
    dotnet publish $installerProject @installerProps `
        "-p:IntermediateOutputPath=obj\uninstaller\" "-p:OutputPath=bin\uninstaller\" `
        @commonProps `
        -o $distUninstDir
}
$builtUninstaller = Join-Path $distUninstDir "SpeedScanInstaller.exe"
if (-not (Test-Path $builtUninstaller)) { throw "SpeedScanInstaller.exe missing in $distUninstDir." }

# --- 4) Publish installer with embedded payload and uninstaller ---
Write-Host "`n[4/4] Publishing installer (payload + uninstaller embedded)..." -ForegroundColor Yellow
Invoke-Checked "Installer publish" {
    dotnet publish $installerProject @installerProps `
        "-p:AppPayloadZip=$portableZip" `
        "-p:UninstallerExe=$builtUninstaller" `
        @commonProps `
        -o $distInstDir
}
$builtInstaller = Join-Path $distInstDir "SpeedScanInstaller.exe"
if (-not (Test-Path $builtInstaller)) { throw "SpeedScanInstaller.exe missing in $distInstDir." }
Copy-Item -Path $builtInstaller -Destination $installerExe -Force

# --- Summary ---
Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Release folder: $releaseDir"
Get-ChildItem $releaseDir -File | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Write-Host " - $($_.Name) ($sizeMB MB)"
}
$uninstMB = [math]::Round((Get-Item $builtUninstaller).Length / 1MB, 1)
Write-Host " (embedded uninstall.exe: $uninstMB MB)"
