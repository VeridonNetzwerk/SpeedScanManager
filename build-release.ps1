<#
.SYNOPSIS
    Builds SpeedScan Manager as a portable ZIP and as an installer, and packs both into a release folder.

.DESCRIPTION
    1. Publishes SpeedScanManager.csproj as a self-contained single-file win-x86 build,
       and zips it as a portable release.
    2. Publishes installer/SpeedScanInstaller.csproj as a self-contained single-file win-x86 build,
       and copies the installer + its dependencies into the release folder.
    3. Both artifacts end up in ./release/vX.X.X/

.PARAMETER Version
    Optional override for the version used in artifact filenames. If omitted, the version is
    read from SpeedScanManager.csproj (<Version> property).
#>
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Get-ProjectVersion {
    $csproj = Join-Path $root "SpeedScanManager.csproj"
    [xml]$xml = Get-Content $csproj
    $ver = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if (-not $ver) { $ver = "0.0.0" }
    return $ver
}

if (-not $Version) {
    $Version = Get-ProjectVersion
}

Write-Host "=== SpeedScan Manager Release Build v$Version ===" -ForegroundColor Cyan

$distDir      = Join-Path $root "dist"
$distInstDir  = Join-Path $root "dist-installer"
$releaseDir   = Join-Path $root "release\v$Version"
$portableZip  = Join-Path $releaseDir "SpeedScanManager-$Version-portable-win-x86.zip"
$installerDir = Join-Path $releaseDir "SpeedScanManager-$Version-installer-win-x86"

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
        $portableZip  = Join-Path $releaseDir "SpeedScanManager-$Version-portable-win-x86.zip"
        $installerDir = Join-Path $releaseDir "SpeedScanManager-$Version-installer-win-x86"
    }
}
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

# --- 1) Build portable app ---
Write-Host "`n[1/4] Publishing portable app..." -ForegroundColor Yellow
dotnet publish (Join-Path $root "SpeedScanManager.csproj") `
    -c Release -r win-x86 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $distDir
if ($LASTEXITCODE -ne 0) { throw "App build failed." }

Write-Host "[2/4] Zipping portable app -> $portableZip" -ForegroundColor Yellow
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $portableZip -Force

# --- 2) Build installer ---
Write-Host "`n[3/4] Publishing installer..." -ForegroundColor Yellow
dotnet publish (Join-Path $root "installer\SpeedScanInstaller.csproj") `
    -c Release -r win-x86 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $distInstDir
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

Write-Host "[4/4] Copying installer -> $installerDir" -ForegroundColor Yellow
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
Copy-Item -Path (Join-Path $distInstDir "*") -Destination $installerDir -Recurse -Force

# --- Summary ---
Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Release folder: $releaseDir"
Get-ChildItem $releaseDir -Recurse -File | Where-Object { $_.DirectoryName -eq $releaseDir -or $_.Extension -eq ".exe" } | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Write-Host " - $($_.FullName.Substring($releaseDir.Length + 1)) ($sizeMB MB)"
}
