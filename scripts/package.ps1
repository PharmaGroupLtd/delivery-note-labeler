# Create a release package that can be copied to other Windows PCs.
# Target PCs need only Windows 10/11 x64. No SDK, Visual Studio, or .NET install.

param(
    [string]$Version,
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $ProjectRoot

$project = Join-Path $ProjectRoot "src\DeliveryNoteLabeler\DeliveryNoteLabeler.csproj"
if (-not $Version) {
    [xml]$projectXml = Get-Content $project
    $Version = ($projectXml.Project.PropertyGroup |
        Where-Object { $_.Version } |
        Select-Object -First 1).Version
    if (-not $Version) {
        $Version = "1.0.0"
    }
}

$publishDir = Join-Path $ProjectRoot "dist\publish"
$packageName = "DeliveryNoteLabeler-$Version-$Runtime"
$packageDir = Join-Path $ProjectRoot "dist\$packageName"
$zipPath = Join-Path $ProjectRoot "dist\$packageName.zip"
$installTemplateDir = Join-Path $ProjectRoot "packaging\install"
$githubRepoFile = Join-Path $ProjectRoot "packaging\update\github-repo.txt"
$githubRepo = if (Test-Path $githubRepoFile) {
    (Get-Content $githubRepoFile -Raw).Trim()
} else {
    "PharmaGroupLtd/delivery-note-labeler"
}

$releaseExcludeFiles = @(
    "DeliveryNoteLabeler.Sparse.msix",
    "DeliveryNoteLabelerShell.dll",
    "DeliveryNoteLabelerShell.lib",
    "DeliveryNoteLabelerShell.exp",
    "DeliveryNoteLabelerShell.pdb",
    "sample-label.zpl",
    "Register-SparsePackage.ps1",
    "Trust-PackageCertificate.ps1",
    "Setup.ps1",
    "DeliveryNoteLabelerPackage.cer"
)

$installFiles = @(
    "Install.ps1",
    "Install.cmd",
    "Uninstall.ps1",
    "PrintLabels.cmd",
    "PrintLabels.ps1",
    "README.txt"
)

if (-not $SkipBuild) {
    & (Join-Path $ProjectRoot "scripts\build.ps1") -SelfContained -Runtime $Runtime
}

if (-not (Test-Path (Join-Path $publishDir "DeliveryNoteLabeler.exe"))) {
    throw "Built app not found at $publishDir. Run scripts\build.ps1 first."
}

Write-Host "Creating release package: $packageName"

if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $packageDir -Recurse -Force

foreach ($fileName in $releaseExcludeFiles) {
    Remove-Item (Join-Path $packageDir $fileName) -Force -ErrorAction SilentlyContinue
}

Get-ChildItem -Path $packageDir -Filter "*.pdb" -File -Recurse | Remove-Item -Force

foreach ($fileName in $installFiles) {
    Copy-Item (Join-Path $installTemplateDir $fileName) (Join-Path $packageDir $fileName) -Force
}

if (-not (Test-Path (Join-Path $packageDir "DeliveryNoteLabeler.exe"))) {
    throw "Release package is missing DeliveryNoteLabeler.exe"
}

Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zipPath -Force

$setupExePath = Join-Path $ProjectRoot "dist\DeliveryNoteLabeler-$Version-Setup.exe"
$setupLauncherDir = Join-Path $ProjectRoot "tools\SetupLauncher"
$setupLauncherZip = Join-Path $setupLauncherDir "DeliveryNoteLabeler-package.zip"
Copy-Item $zipPath $setupLauncherZip -Force

Write-Host "Building single-file installer..."
dotnet publish (Join-Path $setupLauncherDir "SetupLauncher.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o (Join-Path $ProjectRoot "dist\setup-launcher")

$publishedSetup = Join-Path $ProjectRoot "dist\setup-launcher\SetupLauncher.exe"
if (-not (Test-Path $publishedSetup)) {
    throw "Setup launcher was not created at $publishedSetup"
}

Copy-Item $publishedSetup $setupExePath -Force
Remove-Item (Join-Path $ProjectRoot "dist\setup-launcher") -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $setupLauncherZip) {
    Remove-Item $setupLauncherZip -Force -ErrorAction SilentlyContinue
}

Write-Host "Verifying release package install script..."
$verifyRoot = Join-Path $env:TEMP ("DeliveryNoteLabelerVerify-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $verifyRoot -Force | Out-Null
try {
    Expand-Archive -Path $zipPath -DestinationPath $verifyRoot -Force
    $verifyScript = Join-Path $verifyRoot "Install.ps1"
    if (-not (Test-Path $verifyScript)) {
        throw "Release zip is missing Install.ps1 at the top level."
    }
}
finally {
    Remove-Item $verifyRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$latestManifestPath = Join-Path $ProjectRoot "packaging\update\latest.json"
$latestManifest = @{
    version      = $Version
    releaseDate  = (Get-Date).ToString("yyyy-MM-dd")
    downloadUrl  = "https://github.com/$githubRepo/releases/download/v$Version/DeliveryNoteLabeler-$Version-Setup.exe"
    releaseNotes = "Delivery Note Labeler $Version"
}
($latestManifest | ConvertTo-Json -Depth 3) + [Environment]::NewLine |
    Set-Content -Path $latestManifestPath -Encoding UTF8 -NoNewline
Copy-Item $latestManifestPath (Join-Path $ProjectRoot "dist\latest.json") -Force

Write-Host ""
Write-Host "Release package ready:"
Write-Host "  Setup:  $setupExePath"
Write-Host "  Zip:    $zipPath"
Write-Host ""
Write-Host "Copy DeliveryNoteLabeler-$Version-Setup.exe to any Windows 10/11 PC and double-click it."
Write-Host "No SDK, Visual Studio, or .NET install is required on the target PC."
