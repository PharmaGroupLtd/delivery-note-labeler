# Configure where installed apps look for updates.

param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestUrl
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$manifestUrlFile = Join-Path $ProjectRoot "packaging\update\manifest-url.txt"

Set-Content -Path $manifestUrlFile -Value $ManifestUrl.Trim() -Encoding UTF8 -NoNewline

Write-Host "Saved update manifest URL:"
Write-Host "  $ManifestUrl"
Write-Host ""
Write-Host "Rebuild and republish the app so the URL is embedded in new installers:"
Write-Host "  .\scripts\package.ps1"
