# Install Delivery Note Labeler from a release package folder.
# Works on any 64-bit Windows 10/11 PC. No SDK, Visual Studio, .NET, or admin rights required.

$ErrorActionPreference = "Stop"

$PackageRoot = $PSScriptRoot
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Delivery Note Labeler"
$ExePath = Join-Path $InstallDir "DeliveryNoteLabeler.exe"
$LaunchCmdPath = Join-Path $InstallDir "PrintLabels.cmd"
$SkipWhenCopying = @(
    "Install.ps1",
    "Install.cmd",
    "Uninstall.ps1",
    "README.txt"
)

if (-not (Test-Path (Join-Path $PackageRoot "DeliveryNoteLabeler.exe"))) {
    throw "DeliveryNoteLabeler.exe was not found next to this script. Run DeliveryNoteLabeler-Setup.exe or extract the full release zip first."
}

Write-Host "Installing Delivery Note Labeler..."
Write-Host "  From: $PackageRoot"
Write-Host "  To:   $InstallDir"

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Get-ChildItem -Path $PackageRoot -Force |
    Where-Object { $SkipWhenCopying -notcontains $_.Name } |
    ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $InstallDir -Recurse -Force
    }

if (-not (Test-Path $ExePath)) {
    throw "Installation failed: $ExePath was not created."
}

if (-not (Test-Path $LaunchCmdPath)) {
    throw "Installation failed: PrintLabels.cmd was not copied to $LaunchCmdPath"
}

if (-not (Test-Path (Join-Path $InstallDir "PrintLabels.ps1"))) {
    throw "Installation failed: PrintLabels.ps1 was not copied to the install folder."
}

Register-PrintLabelsContextMenu -LaunchCmdPath $LaunchCmdPath -IconPath (Join-Path $InstallDir "DeliveryNoteLabeler.ico") -ExePath $ExePath

Write-Host ""
Write-Host "Installation complete."
Write-Host "  App:  $ExePath"
Write-Host "  Menu: Right-click PDF(s) in File Explorer -> Print Labels"
Write-Host "        (on Windows 11 this may appear under 'Show more options')"
Write-Host ""
Write-Host "Configure your Zebra printer in Settings before printing."

function Register-PrintLabelsContextMenu {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LaunchCmdPath,

        [Parameter(Mandatory = $true)]
        [string]$IconPath,

        [Parameter(Mandatory = $true)]
        [string]$ExePath
    )

    if (-not (Test-Path $IconPath)) {
        $IconPath = "$ExePath,0"
    }

    $command = "`"$LaunchCmdPath`" %*"
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
