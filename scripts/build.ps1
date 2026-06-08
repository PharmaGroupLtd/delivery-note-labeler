# Build Delivery Note Labeler (.NET 8 WPF publish output)

param(
    [switch]$SelfContained,
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $ProjectRoot

$publishDir = Join-Path $ProjectRoot "dist\publish"
$project = Join-Path $ProjectRoot "src\DeliveryNoteLabeler\DeliveryNoteLabeler.csproj"
$selfContainedFlag = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing WPF app (self-contained: $selfContainedFlag, runtime: $Runtime)..."
dotnet publish $project -c Release -r $Runtime --self-contained $selfContainedFlag -o $publishDir

$exePath = Join-Path $publishDir "DeliveryNoteLabeler.exe"
if (-not (Test-Path $exePath)) {
    throw "Build failed: $exePath was not created."
}

$iconSource = Join-Path $ProjectRoot "assets\DeliveryNoteLabeler.ico"
if (Test-Path $iconSource) {
    Copy-Item $iconSource (Join-Path $publishDir "DeliveryNoteLabeler.ico") -Force
}

$logoSource = Join-Path $ProjectRoot "assets\logo.png"
$publishAssetsDir = Join-Path $publishDir "assets"
if (Test-Path $logoSource) {
    New-Item -ItemType Directory -Path $publishAssetsDir -Force | Out-Null
    Copy-Item $logoSource (Join-Path $publishAssetsDir "logo.png") -Force
    Write-Host "Copied logo to $publishAssetsDir\logo.png"
}
else {
    Write-Warning "Logo not found at $logoSource"
}

Write-Host ""
Write-Host "Build complete:"
Write-Host "  $exePath"
Write-Host ""
Write-Host "Next step:"
Write-Host "  Dev machine:  scripts\install.ps1"
Write-Host "  Other PCs:    scripts\package.ps1, then copy dist\DeliveryNoteLabeler-* and run Install.ps1"
