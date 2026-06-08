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
$shellProject = Join-Path $ProjectRoot "src\DeliveryNoteLabeler.ShellExtension\DeliveryNoteLabeler.ShellExtension.vcxproj"
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

Write-Host "Generating sample label ZPL..."
dotnet run --project (Join-Path $ProjectRoot "tools\GenerateSampleLabel\GenerateSampleLabel.csproj") -- $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Sample label ZPL was not generated."
}

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
if ($msbuild -and (Test-Path $shellProject)) {
    Write-Host "Building Windows 11 shell extension..."
    & $msbuild $shellProject /p:Configuration=Release /p:Platform=x64 /restore
    if ($LASTEXITCODE -ne 0) {
        throw "Shell extension build failed."
    }
}
else {
    Write-Warning "MSBuild was not found. The Windows 11 context menu DLL was not built. Install Visual Studio Build Tools with the C++ desktop workload."
}

$shellDll = Join-Path $publishDir "DeliveryNoteLabelerShell.dll"
if (Test-Path $shellDll) {
    try {
        Write-Host "Building Windows 11 sparse package..."
        & (Join-Path $ProjectRoot "scripts\build-sparse-package.ps1") `
            -InstallDir $publishDir `
            -OutputPackagePath (Join-Path $publishDir "DeliveryNoteLabeler.Sparse.msix")
    }
    catch {
        Write-Warning "Windows 11 sparse package was not built: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Build complete:"
Write-Host "  $exePath"
if (Test-Path $shellDll) {
    Write-Host "  $shellDll"
}
Write-Host ""
Write-Host "Next step:"
Write-Host "  Dev machine:  scripts\install.ps1"
Write-Host "  Other PCs:    scripts\package.ps1, then copy dist\DeliveryNoteLabeler-* and run Install.ps1"
