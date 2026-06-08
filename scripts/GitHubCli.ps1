# Resolve GitHub CLI (gh) even when it is not on PATH yet in this PowerShell session.

function Resolve-GhCli {
    $command = Get-Command gh -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles "GitHub CLI\gh.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "GitHub CLI\gh.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\GitHub CLI\gh.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Test-GhCliAuthenticated {
    param(
        [Parameter(Mandatory = $true)]
        [string]$GhPath
    )

    $output = & $GhPath auth status 2>&1
    return $LASTEXITCODE -eq 0 -and ($output -join " ") -notmatch "not logged into any GitHub hosts"
}
