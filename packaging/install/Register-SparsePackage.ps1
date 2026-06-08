# Register or remove the Windows 11 sparse package for the Explorer context menu.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Install", "Uninstall")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$InstallDir,

    [string]$CertificatePath
)

$ErrorActionPreference = "Stop"

function Get-InstalledSparsePackage {
    Get-AppxPackage -Name "DeliveryNoteLabeler" -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Import-PackageCertificate {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "Package certificate not found at $Path"
    }

    $trustedPeople = "Cert:\LocalMachine\TrustedPeople"
    $existing = Get-ChildItem $trustedPeople -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq "CN=Delivery Note Labeler" } |
        Select-Object -First 1
    if ($existing) {
        return
    }

    Import-Certificate -FilePath $Path -CertStoreLocation $trustedPeople | Out-Null
}

if ($Action -eq "Install") {
    $packagePath = Join-Path $InstallDir "DeliveryNoteLabeler.Sparse.msix"
    if (-not (Test-Path $packagePath)) {
        throw "Sparse package not found at $packagePath."
    }

    $existing = Get-InstalledSparsePackage
    if ($existing) {
        Remove-AppxPackage -Package $existing.PackageFullName
    }

    if ($CertificatePath -and (Test-Path $CertificatePath)) {
        try {
            Import-PackageCertificate -Path $CertificatePath
        }
        catch {
            throw "Could not trust the package signing certificate. Run Trust-PackageCertificate.ps1 as Administrator, then run Install.ps1 again."
        }
    }
    else {
        $subject = "CN=Delivery Note Labeler"
        $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject } | Select-Object -First 1
        if ($cert) {
            $cerPath = Join-Path $env:TEMP ("DeliveryNoteLabelerPackage-" + $cert.Thumbprint + ".cer")
            Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
            try {
                Import-PackageCertificate -Path $cerPath
            }
            finally {
                if (Test-Path $cerPath) {
                    Remove-Item $cerPath -Force
                }
            }
        }
    }

    Add-AppxPackage -Path $packagePath -ExternalLocation $InstallDir | Out-Null
    Write-Host "Registered Windows 11 context menu package."
    return
}

$existing = Get-InstalledSparsePackage
if ($existing) {
    Remove-AppxPackage -Package $existing.PackageFullName
    Write-Host "Removed Windows 11 context menu package."
}
else {
    Write-Host "Windows 11 context menu package was not registered."
}
