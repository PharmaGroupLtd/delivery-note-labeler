# Install Delivery Note Labeler and register the Explorer "Print Labels" context menu.

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "InstallUtils.ps1")

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SourceDir = Join-Path $ProjectRoot "dist\publish"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Delivery Note Labeler"
$ExePath = Join-Path $InstallDir "DeliveryNoteLabeler.exe"
$SkipNames = [System.Collections.Generic.HashSet[string]]::new([string[]](Get-InstallSkipNames), [StringComparer]::OrdinalIgnoreCase)

if (-not (Test-Path (Join-Path $SourceDir "DeliveryNoteLabeler.exe"))) {
    Write-Host "Built app not found. Building self-contained release output..."
    & (Join-Path $ProjectRoot "scripts\build.ps1") -SelfContained
}

Write-Host "Installing to: $InstallDir"

Get-Process DeliveryNoteLabeler -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Get-ChildItem -Path $SourceDir -Force |
    Where-Object { $SkipNames -notcontains $_.Name } |
    ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $InstallDir -Recurse -Force
    }

Remove-InstallBloat -InstallDir $InstallDir

if (-not (Test-Path $ExePath)) {
    throw "Installation failed: $ExePath was not created."
}

Register-PrintLabelsContextMenu -AppExePath $ExePath -IconPath (Join-Path $InstallDir "DeliveryNoteLabeler.ico")

Write-Host ""
Write-Host "Installation complete."
Write-Host "  App:      $ExePath"
Write-Host "  Menu:     Right-click PDF(s) in File Explorer -> Print Labels"
Write-Host "            On Windows 11, check 'Show more options' if it is not on the main menu."
Write-Host ""
Write-Host "If the menu does not appear, run: scripts\refresh-print-labels.ps1"
