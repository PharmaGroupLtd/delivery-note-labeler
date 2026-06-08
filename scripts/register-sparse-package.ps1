# Register or remove the Windows 11 sparse package for the Explorer context menu.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Install", "Uninstall")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$InstallDir
)

$ErrorActionPreference = "Stop"

function Get-InstalledSparsePackage {
    Get-AppxPackage -Name "DeliveryNoteLabeler" -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Ensure-PackageCertificateTrusted {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )

    $trustedPeople = "Cert:\LocalMachine\TrustedPeople"
    $existing = Get-ChildItem $trustedPeople -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint } |
        Select-Object -First 1
    if ($existing) {
        return
    }

    $cerPath = Join-Path $env:TEMP ("DeliveryNoteLabelerPackage-" + $Certificate.Thumbprint + ".cer")
    Export-Certificate -Cert $Certificate -FilePath $cerPath | Out-Null
    try {
        Import-Certificate -FilePath $cerPath -CertStoreLocation $trustedPeople | Out-Null
    }
    catch {
        throw "Could not trust the package signing certificate in LocalMachine\TrustedPeople. Run PowerShell as Administrator and execute scripts\trust-package-certificate.ps1, then re-run scripts\install.ps1."
    }
    finally {
        if (Test-Path $cerPath) {
            Remove-Item $cerPath -Force
        }
    }
}

if ($Action -eq "Install") {
    $packagePath = Join-Path $InstallDir "DeliveryNoteLabeler.Sparse.msix"
    if (-not (Test-Path $packagePath)) {
        throw "Sparse package not found at $packagePath. Re-run scripts\build.ps1 on a machine with the Windows SDK installed."
    }

    $existing = Get-InstalledSparsePackage
    if ($existing) {
        Remove-AppxPackage -Package $existing.PackageFullName
    }

    $subject = "CN=Delivery Note Labeler"
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject } | Select-Object -First 1
    if ($cert) {
        Ensure-PackageCertificateTrusted -Certificate $cert
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
