# Install Delivery Note Labeler and register the Explorer "Print Labels" context menu.

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SourceDir = Join-Path $ProjectRoot "dist\publish"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Delivery Note Labeler"
$ExePath = Join-Path $InstallDir "DeliveryNoteLabeler.exe"
$LaunchCmdPath = Join-Path $InstallDir "PrintLabels.cmd"
$LaunchPs1Path = Join-Path $InstallDir "PrintLabels.ps1"
$InstallTemplateDir = Join-Path $ProjectRoot "packaging\install"

if (-not (Test-Path (Join-Path $SourceDir "DeliveryNoteLabeler.exe"))) {
    Write-Host "Built app not found. Building self-contained release output..."
    & (Join-Path $ProjectRoot "scripts\build.ps1") -SelfContained
}

if (-not (Test-Path (Join-Path $SourceDir "DeliveryNoteLabeler.exe"))) {
    throw "Built app not found. Run scripts\build.ps1 first."
}

Write-Host "Installing to: $InstallDir"

if (Test-Path $InstallDir) {
    Get-Process DeliveryNoteLabeler -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    Remove-Item -Path $InstallDir -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $SourceDir "*") -Destination $InstallDir -Recurse -Force
Copy-Item -Path (Join-Path $InstallTemplateDir "PrintLabels.cmd") $LaunchCmdPath -Force
Copy-Item -Path (Join-Path $InstallTemplateDir "PrintLabels.ps1") $LaunchPs1Path -Force

$iconPath = Join-Path $InstallDir "DeliveryNoteLabeler.ico"
if (-not (Test-Path $iconPath)) {
    $iconPath = "$ExePath,0"
}

$command = "`"$LaunchCmdPath`" %*"
$registryPaths = @(
    "Registry::HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\.pdf\shell\PrintLabels",
    "Registry::HKEY_CURRENT_USER\Software\Classes\.pdf\shell\PrintLabels"
)

foreach ($shellKey in $registryPaths) {
    New-Item -Path $shellKey -Force | Out-Null
    Set-ItemProperty -Path $shellKey -Name "(Default)" -Value "Print Labels"
    Set-ItemProperty -Path $shellKey -Name "Icon" -Value $iconPath
    Set-ItemProperty -Path $shellKey -Name "MultiSelectModel" -Value "Document"

    $commandKey = Join-Path $shellKey "command"
    New-Item -Path $commandKey -Force | Out-Null
    Set-ItemProperty -Path $commandKey -Name "(Default)" -Value $command
}

Write-Host ""
Write-Host "Installation complete."
Write-Host "  App:      $ExePath"
Write-Host "  Menu:     Right-click PDF(s) in File Explorer -> Print Labels"
Write-Host "            On Windows 11, check 'Show more options' if it is not on the main menu."
Write-Host ""
Write-Host "Note: Configure the shared GK420d in Settings before printing labels."
