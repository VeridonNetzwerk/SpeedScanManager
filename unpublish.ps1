<#
.SYNOPSIS
    Remove Mark-of-the-Web (Zone.Identifier) from published SpeedScanManager files.
.DESCRIPTION
    Runs Unblock-File on all files in the publish directory.
    Meant to be called after 'dotnet publish' in CI/CD pipelines.
.PARAMETER PublishDir
    Path to the published output directory.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

Write-Host "Unblocking files in: $PublishDir" -ForegroundColor Cyan

$files = Get-ChildItem -Path $PublishDir -File -Recurse |
         Where-Object { $_.Extension -match '\.(exe|dll)$' }

foreach ($file in $files) {
    try {
        Unblock-File -LiteralPath $file.FullName -ErrorAction Stop
        Write-Host "  Unblocked: $($file.Name)" -ForegroundColor Green
    }
    catch {
        Write-Warning "  Failed to unblock: $($file.Name) -- $($_.Exception.Message)"
    }
}

Write-Host "Done. All files in $PublishDir are now unblocked." -ForegroundColor Yellow
