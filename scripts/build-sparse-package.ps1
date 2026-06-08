# Build the sparse identity package used for the Windows 11 context menu.

param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputPackagePath
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SparseRoot = Join-Path $ProjectRoot "packaging\sparse"
$StageRoot = Join-Path $env:TEMP ("DeliveryNoteLabelerSparse-" + [Guid]::NewGuid().ToString("N"))

function Get-SdkToolPath {
    param(
        [string]$ToolName
    )

    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10"
    if (-not (Test-Path $kitsRoot)) {
        return $null
    }

    $binRoot = Join-Path $kitsRoot "bin"
    $versionDirs = @()
    if (Test-Path $binRoot) {
        $versionDirs = Get-ChildItem $binRoot -Directory |
            Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
            Sort-Object { [Version]$_.Name } -Descending
    }

    foreach ($versionDir in $versionDirs) {
        foreach ($arch in @("x64", "x86", "arm64")) {
            $toolPath = Join-Path $versionDir.FullName "$arch\$ToolName"
            if (Test-Path $toolPath) {
                return $toolPath
            }
        }
    }

    $appCertKitPath = Join-Path $kitsRoot "App Certification Kit\$ToolName"
    if (Test-Path $appCertKitPath) {
        return $appCertKitPath
    }

    return $null
}

function Ensure-LogoAssets {
    param(
        [string]$AssetsDir
    )

    New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null
    Add-Type -AssemblyName System.Drawing

    function Write-Logo {
        param(
            [int]$Size,
            [string]$Path
        )

        $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 240, 240, 240))
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 26, 26, 26)), 2
        $graphics.DrawRectangle($pen, 4, 4, $Size - 8, $Size - 8)
        $graphics.Dispose()
        $pen.Dispose()
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
    }

    Write-Logo -Size 44 -Path (Join-Path $AssetsDir "Square44x44Logo.png")
    Write-Logo -Size 150 -Path (Join-Path $AssetsDir "Square150x150Logo.png")
}

function Get-PackageCertificate {
    $subject = "CN=Delivery Note Labeler"
    $existing = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject } | Select-Object -First 1
    if ($existing) {
        return $existing
    }

    return New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -FriendlyName "Delivery Note Labeler Package Signing" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @("2.5.4.3=Delivery Note Labeler")
}

try {
    New-Item -ItemType Directory -Path $StageRoot -Force | Out-Null
    Copy-Item (Join-Path $SparseRoot "AppxManifest.xml") (Join-Path $StageRoot "AppxManifest.xml") -Force
    Ensure-LogoAssets -AssetsDir (Join-Path $StageRoot "Assets")

    $makeAppx = Get-SdkToolPath -ToolName "makeappx.exe"
    $signTool = Get-SdkToolPath -ToolName "signtool.exe"
    if (-not $makeAppx -or -not $signTool) {
        throw "Windows SDK tools were not found. Install the Windows 10/11 SDK to build the Windows 11 context menu package."
    }

    $unsignedPackage = [System.IO.Path]::ChangeExtension($OutputPackagePath, ".unsigned.msix")
    if (Test-Path $unsignedPackage) {
        Remove-Item $unsignedPackage -Force
    }
    if (Test-Path $OutputPackagePath) {
        Remove-Item $OutputPackagePath -Force
    }

    & $makeAppx pack /d $StageRoot /p $unsignedPackage /o /nv | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx failed with exit code $LASTEXITCODE"
    }

    $cert = Get-PackageCertificate
    & $signTool sign /fd SHA256 /s My /sha1 $cert.Thumbprint $unsignedPackage | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE"
    }

    Move-Item $unsignedPackage $OutputPackagePath -Force
    Write-Host "Created sparse package: $OutputPackagePath"
}
finally {
    if (Test-Path $StageRoot) {
        Remove-Item $StageRoot -Recurse -Force
    }
}
