# Import the Delivery Note Labeler package signing certificate into the machine trust store.
# Run this script once as Administrator before installing on a new PC (Windows 11 menu only).

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

$PackageRoot = $PSScriptRoot
$BundledCertificate = Join-Path $PackageRoot "DeliveryNoteLabelerPackage.cer"

if (Test-Path $BundledCertificate) {
    Import-Certificate -FilePath $BundledCertificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    Write-Host "Trusted package signing certificate from $BundledCertificate"
    exit 0
}

$subject = "CN=Delivery Note Labeler"
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    throw "Package signing certificate not found. Use a release package that includes DeliveryNoteLabelerPackage.cer."
}

$cerPath = Join-Path $env:TEMP ("DeliveryNoteLabelerPackage-" + $cert.Thumbprint + ".cer")
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
try {
    Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    Write-Host "Trusted package signing certificate in LocalMachine\TrustedPeople."
}
finally {
    if (Test-Path $cerPath) {
        Remove-Item $cerPath -Force
    }
}
