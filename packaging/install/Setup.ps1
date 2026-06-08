# Bootstrap used by manual zip installs.

$ErrorActionPreference = "Stop"

$ExtractDir = Join-Path $env:TEMP ("DeliveryNoteLabeler-Setup-" + [Guid]::NewGuid().ToString("N"))
$PackageRoot = $PSScriptRoot
$ZipPath = Join-Path $PackageRoot "DeliveryNoteLabeler-package.zip"

if (-not (Test-Path $ZipPath)) {
    throw "DeliveryNoteLabeler-package.zip was not found next to Setup.ps1."
}

Write-Host "Preparing Delivery Note Labeler..."
New-Item -ItemType Directory -Path $ExtractDir -Force | Out-Null

try {
    Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir -Force
    $packageRoot = Resolve-PackageRoot -ExtractDir $ExtractDir
    $installScript = Join-Path $packageRoot "Install.ps1"
    if (-not (Test-Path $installScript)) {
        throw "Install.ps1 was not found inside the package zip."
    }

    & $installScript
}
finally {
    if (Test-Path $ExtractDir) {
        Remove-Item -Path $ExtractDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-PackageRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExtractDir
    )

    $directInstallScript = Join-Path $ExtractDir "Install.ps1"
    if (Test-Path $directInstallScript) {
        return $ExtractDir
    }

    $nestedDirectories = Get-ChildItem -Path $ExtractDir -Directory
    if ($nestedDirectories.Count -eq 1) {
        $nestedRoot = $nestedDirectories[0].FullName
        if (Test-Path (Join-Path $nestedRoot "Install.ps1")) {
            return $nestedRoot
        }
    }

    throw "The installer package is invalid. Download a fresh copy and try again."
}
