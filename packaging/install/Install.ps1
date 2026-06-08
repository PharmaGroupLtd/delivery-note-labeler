# Install Delivery Note Labeler from a release package folder.
# Works on any 64-bit Windows 10/11 PC. No SDK, Visual Studio, .NET, or admin rights required.

$ErrorActionPreference = "Stop"

$PackageRoot = $PSScriptRoot
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Delivery Note Labeler"
$ExePath = Join-Path $InstallDir "DeliveryNoteLabeler.exe"
$SkipWhenCopying = @(
    "Install.ps1",
    "Install.cmd",
    "Uninstall.ps1",
    "README.txt",
    "DeliveryNoteLabeler-package.zip",
    "DeliveryNoteLabeler.Sparse.msix",
    "DeliveryNoteLabelerShell.dll",
    "DeliveryNoteLabelerShell.lib",
    "DeliveryNoteLabelerShell.exp",
    "sample-label.zpl",
    "Register-SparsePackage.ps1",
    "Trust-PackageCertificate.ps1",
    "Setup.ps1",
    "DeliveryNoteLabelerPackage.cer",
    "PrintLabels.ps1",
    "PrintLabels.cmd",
    "PrintLabels.exe",
    "PrintLabels.dll",
    "PrintLabels.deps.json",
    "PrintLabels.runtimeconfig.json"
)

if (-not (Test-Path (Join-Path $PackageRoot "DeliveryNoteLabeler.exe"))) {
    throw "DeliveryNoteLabeler.exe was not found next to this script. Run DeliveryNoteLabeler-Setup.exe or extract the full release zip first."
}

Write-Host "Installing Delivery Note Labeler..."
Write-Host "  From: $PackageRoot"
Write-Host "  To:   $InstallDir"

if (Test-Path $InstallDir) {
    Get-Process DeliveryNoteLabeler -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    Remove-Item -Path $InstallDir -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Get-ChildItem -Path $PackageRoot -Force |
    Where-Object { $SkipWhenCopying -notcontains $_.Name } |
    ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $InstallDir -Recurse -Force
    }

foreach ($fileName in $SkipWhenCopying) {
    Remove-Item (Join-Path $InstallDir $fileName) -Force -ErrorAction SilentlyContinue
}

Get-ChildItem -Path $InstallDir -Filter "*.pdb" -File -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

if (-not (Test-Path $ExePath)) {
    throw "Installation failed: $ExePath was not created."
}

Register-PrintLabelsContextMenu -AppExePath $ExePath -IconPath (Join-Path $InstallDir "DeliveryNoteLabeler.ico")

Write-Host ""
Write-Host "Installation complete."
Write-Host "  App:  $ExePath"
Write-Host "  Menu: Right-click PDF(s) in File Explorer -> Print Labels"
Write-Host "        (on Windows 11 this may appear under 'Show more options')"
Write-Host ""
Write-Host "Configure your Zebra printer in Settings before printing."

function Get-PrintLabelsShellCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppExePath
    )

    $placeholders = 1..9 | ForEach-Object { "`"%$_`"" }
    return "`"$AppExePath`" $($placeholders -join ' ')"
}

function Register-PrintLabelsContextMenu {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppExePath,

        [Parameter(Mandatory = $true)]
        [string]$IconPath
    )

    if (-not (Test-Path $AppExePath)) {
        throw "Delivery Note Labeler executable was not found: $AppExePath"
    }

    if (-not (Test-Path $IconPath)) {
        $IconPath = "$AppExePath,0"
    }

    $command = Get-PrintLabelsShellCommand -AppExePath $AppExePath
    $registryPaths = @(
        "Registry::HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\.pdf\shell\PrintLabels",
        "Registry::HKEY_CURRENT_USER\Software\Classes\.pdf\shell\PrintLabels"
    )

    foreach ($shellKey in $registryPaths) {
        New-Item -Path $shellKey -Force | Out-Null
        Set-ItemProperty -Path $shellKey -Name "(Default)" -Value "Print Labels"
        Set-ItemProperty -Path $shellKey -Name "Icon" -Value $IconPath
        Set-ItemProperty -Path $shellKey -Name "MultiSelectModel" -Value "Document"

        $commandKey = Join-Path $shellKey "command"
        New-Item -Path $commandKey -Force | Out-Null
        Set-ItemProperty -Path $commandKey -Name "(Default)" -Value $command
    }
}
