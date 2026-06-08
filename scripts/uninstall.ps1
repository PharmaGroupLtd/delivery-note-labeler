# Remove Delivery Note Labeler install folder and Explorer context menu entry.

$ErrorActionPreference = "Stop"

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Delivery Note Labeler"

$registryPaths = @(
    "Registry::HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\.pdf\shell\PrintLabels",
    "Registry::HKEY_CURRENT_USER\Software\Classes\.pdf\shell\PrintLabels"
)

foreach ($path in $registryPaths) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
        Write-Host "Removed Explorer context menu entry."
    }
}

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "Removed install folder: $InstallDir"
} else {
    Write-Host "Install folder not found: $InstallDir"
}

Write-Host "Uninstall complete."
