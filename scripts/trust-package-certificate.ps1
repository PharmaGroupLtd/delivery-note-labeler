# Import the Delivery Note Labeler package signing certificate into the machine trust store.
# Run this script once as Administrator before registering the sparse package.

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

$subject = "CN=Delivery Note Labeler"
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    throw "Package signing certificate not found. Run scripts\build.ps1 first."
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
