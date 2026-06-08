# Build a release package and publish it online via GitHub Releases + GitHub Pages.
#
# Automatic publish (needs GitHub CLI + login):
#   .\scripts\publish-release.ps1 -ReleaseNotes "Initial public release."
#
# Manual publish (no gh required):
#   .\scripts\publish-release.ps1 -Manual -ReleaseNotes "Initial public release."
#
# One-time setup for automatic publish:
#   1. GitHub CLI is installed (winget install GitHub.cli)
#   2. Run: gh auth login
#   3. Enable GitHub Pages (Settings -> Pages -> deploy from /docs on main)

param(
    [string]$Repo,
    [string]$Version,
    [string]$ReleaseNotes = "",
    [switch]$SkipBuild,
    [switch]$Manual
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $ProjectRoot

. (Join-Path $PSScriptRoot "GitHubCli.ps1")
. (Join-Path $PSScriptRoot "Get-ProjectVersion.ps1")

$githubRepoFile = Join-Path $ProjectRoot "packaging\update\github-repo.txt"
if (-not $Repo) {
    if (Test-Path $githubRepoFile) {
        $Repo = (Get-Content $githubRepoFile -Raw).Trim()
    } else {
        $Repo = "PharmaGroupLtd/delivery-note-labeler"
    }
}

$project = Join-Path $ProjectRoot "src\DeliveryNoteLabeler\DeliveryNoteLabeler.csproj"
if (-not $Version) {
    $Version = Get-ProjectVersion -ProjectRoot $ProjectRoot
}

$setupName = "DeliveryNoteLabeler-$Version-Setup.exe"
$setupPath = Join-Path $ProjectRoot "dist\$setupName"
$tag = "v$Version"
$downloadUrl = "https://github.com/$Repo/releases/download/$tag/$setupName"
$pagesBaseUrl = "https://$($Repo.Split('/')[0]).github.io/$($Repo.Split('/')[1])"
$manifestUrl = "$pagesBaseUrl/latest.json"

if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $ReleaseNotes = "Delivery Note Labeler $Version"
}

function Update-ReleaseManifests {
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
}

Write-Host "Building release package..."
& (Join-Path $ProjectRoot "scripts\package.ps1") -Version $Version -SkipBuild:$SkipBuild

if (-not (Test-Path $setupPath)) {
    throw "Setup file was not created: $setupPath"
}

Update-ReleaseManifests

Write-Host ""
Write-Host "Rebuilding installer with embedded update URL..."
& (Join-Path $ProjectRoot "scripts\package.ps1") -Version $Version -SkipBuild

if (-not (Test-Path $setupPath)) {
    throw "Setup file was not created after rebuild: $setupPath"
}

$ghPath = Resolve-GhCli
$canUseGh = $false
if (-not $Manual -and $ghPath) {
    $canUseGh = Test-GhCliAuthenticated -GhPath $ghPath
}

if (-not $Manual -and -not $ghPath) {
    Write-Host ""
    Write-Host "GitHub CLI was not found on PATH."
    Write-Host "Install it with: winget install GitHub.cli"
    Write-Host "Falling back to manual publish instructions."
    $Manual = $true
}
elseif (-not $Manual -and -not $canUseGh) {
    Write-Host ""
    Write-Host "GitHub CLI is installed but you are not logged in."
    Write-Host "Run this once, then re-run publish-release.ps1:"
    Write-Host "  `"$ghPath`" auth login"
    Write-Host ""
    Write-Host "Falling back to manual publish instructions."
    $Manual = $true
}

if ($Manual) {
    $releaseNewUrl = "https://github.com/$Repo/releases/new?tag=$tag&title=Delivery+Note+Labeler+$Version"

    Write-Host ""
    Write-Host "Manual publish ready:"
    Write-Host "  Installer:    $setupPath"
    Write-Host "  Release tag:  $tag"
    Write-Host "  Download URL: $downloadUrl"
    Write-Host "  Update check: $manifestUrl"
    Write-Host "  Download page:$pagesBaseUrl"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. Commit and push the updated manifest files:"
    Write-Host "       git add docs/latest.json packaging/update/latest.json packaging/update/manifest-url.txt"
    Write-Host "       git commit -m `"Publish release $Version`""
    Write-Host "       git push"
    Write-Host "  2. Create the GitHub release and upload the installer:"
    Write-Host "       $releaseNewUrl"
    Write-Host "     Upload: $setupName"
    Write-Host "  3. Confirm GitHub Pages is enabled (Settings -> Pages -> /docs on main)."
    Write-Host ""
    Write-Host "Opening the GitHub release page in your browser..."

    Start-Process $releaseNewUrl
    return
}

Write-Host ""
Write-Host "Publishing GitHub release $tag ..."

$releaseArgs = @(
    "release", "create", $tag,
    $setupPath,
    (Join-Path $ProjectRoot "docs\latest.json"),
    "--repo", $Repo,
    "--title", "Delivery Note Labeler $Version",
    "--notes", $ReleaseNotes
)

& $ghPath @releaseArgs

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
