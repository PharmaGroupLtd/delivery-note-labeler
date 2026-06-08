function Get-InstallSkipNames {
    @(
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
}

function Get-PrintLabelsShellCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppExePath
    )

    # Explorer expands %1..%9 for selected PDF paths. %* is NOT expanded for direct .exe handlers.
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

function Remove-InstallBloat {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallDir
    )

    foreach ($fileName in Get-InstallSkipNames) {
        Remove-Item (Join-Path $InstallDir $fileName) -Force -ErrorAction SilentlyContinue
    }

    Get-ChildItem -Path $InstallDir -Filter "*.pdb" -File -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
}
