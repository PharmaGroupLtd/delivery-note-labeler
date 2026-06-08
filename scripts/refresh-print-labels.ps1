# Re-register the Explorer "Print Labels" menu without reinstalling the app.

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "InstallUtils.ps1")

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Delivery Note Labeler"
$AppExePath = Join-Path $InstallDir "DeliveryNoteLabeler.exe"
$IconPath = Join-Path $InstallDir "DeliveryNoteLabeler.ico"

if (-not (Test-Path $AppExePath)) {
    throw "DeliveryNoteLabeler.exe was not found at $AppExePath. Run scripts\install.ps1 first."
}

Register-PrintLabelsContextMenu -AppExePath $AppExePath -IconPath $IconPath

Write-Host "Print Labels context menu refreshed."
Write-Host "  Command:  $(Get-PrintLabelsShellCommand -AppExePath $AppExePath)"
Write-Host "  Try: right-click a PDF -> Print Labels"
