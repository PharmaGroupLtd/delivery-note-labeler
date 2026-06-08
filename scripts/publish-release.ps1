# Build a release package and publish it online via GitHub Releases + GitHub Pages.
#
# One-time setup:
#   1. Create a GitHub repo and push this project.
#   2. Install GitHub CLI: https://cli.github.com/
#   3. Run: gh auth login
#   4. Enable GitHub Pages for the repo (Settings -> Pages -> deploy from /docs).
#   5. Run this script with your repo name.
#
# Example:
#   .\scripts\publish-release.ps1 -Repo "your-org/delivery-note-labeler" -ReleaseNotes "Fixes Print Labels on other PCs."

param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,
    [string]$Version,
    [string]$ReleaseNotes = "",
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
        throw "Could not determine app version from $project"
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is required. Install it from https://cli.github.com/ and run gh auth login."
}

$setupName = "DeliveryNoteLabeler-$Version-Setup.exe"
$setupPath = Join-Path $ProjectRoot "dist\$setupName"
$tag = "v$Version"

& (Join-Path $ProjectRoot "scripts\package.ps1") -Version $Version -SkipBuild:$SkipBuild

if (-not (Test-Path $setupPath)) {
    throw "Setup file was not created: $setupPath"
}

$downloadUrl = "https://github.com/$Repo/releases/download/$tag/$setupName"
$pagesBaseUrl = "https://$($Repo.Split('/')[0]).github.io/$($Repo.Split('/')[1])"
$manifestUrl = "$pagesBaseUrl/latest.json"

if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $ReleaseNotes = "Delivery Note Labeler $Version"
}

$latestManifest = [ordered]@{
    version      = $Version
    releaseDate  = (Get-Date).ToString("yyyy-MM-dd")
    downloadUrl  = $downloadUrl
    releaseNotes = $ReleaseNotes.Trim()
}
$latestJson = ($latestManifest | ConvertTo-Json -Depth 3) + [Environment]::NewLine

$repoLatestPath = Join-Path $ProjectRoot "packaging\update\latest.json"
$docsLatestPath = Join-Path $ProjectRoot "docs\latest.json"
Set-Content -Path $repoLatestPath -Value $latestJson -Encoding UTF8 -NoNewline
Copy-Item $repoLatestPath $docsLatestPath -Force

& (Join-Path $ProjectRoot "scripts\configure-update-url.ps1") -ManifestUrl $manifestUrl

Write-Host ""
Write-Host "Publishing GitHub release $tag ..."

$releaseArgs = @(
    "release", "create", $tag,
    $setupPath,
    $docsLatestPath,
    "--repo", $Repo,
    "--title", "Delivery Note Labeler $Version",
    "--notes", $ReleaseNotes
)

& gh @releaseArgs

Write-Host ""
Write-Host "Published:"
Write-Host "  Release:      https://github.com/$Repo/releases/tag/$tag"
Write-Host "  Download:     $downloadUrl"
Write-Host "  Update check: $manifestUrl"
Write-Host "  Download page:$pagesBaseUrl"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Commit docs/latest.json, packaging/update/latest.json, and packaging/update/manifest-url.txt."
Write-Host "  2. Push to GitHub so Pages serves docs/index.html and docs/latest.json."
Write-Host "  3. Rebuild Setup.exe so the embedded update URL is included:"
Write-Host "       .\scripts\package.ps1"
Write-Host "  4. Re-upload the rebuilt Setup.exe to the same GitHub release if needed."

Write-Host ""
Write-Host "Rebuilding installer with embedded update URL..."
& (Join-Path $ProjectRoot "scripts\package.ps1") -Version $Version -SkipBuild
Write-Host "Rebuild complete: $setupPath"
