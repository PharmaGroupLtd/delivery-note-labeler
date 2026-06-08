# Explorer "Print Labels" launcher. Writes selected PDF paths to a temp list file
# and starts DeliveryNoteLabeler.exe with --open-from (handles spaces and special chars).

$ErrorActionPreference = 'Stop'

$exeDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $exeDir 'DeliveryNoteLabeler.exe'
$logDir = Join-Path $env:LOCALAPPDATA 'Delivery Note Labeler'
$logPath = Join-Path $logDir 'print-labels.log'

function Write-LauncherLog {
    param([string]$Message)

    try {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
        Add-Content -Path $logPath -Value $line -Encoding UTF8
    }
    catch {
        # Ignore logging failures.
    }
}

try {
    $pdfPaths = @($args | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    Write-LauncherLog ("Launch args ({0}): {1}" -f $pdfPaths.Count, ($pdfPaths -join ' | '))

    if ($pdfPaths.Count -eq 0) {
        Write-LauncherLog 'No PDF paths were passed from Explorer.'
        exit 0
    }

    if (-not (Test-Path -LiteralPath $exePath)) {
        Write-LauncherLog "Missing executable: $exePath"
        exit 1
    }

    $listPath = Join-Path $env:TEMP ("dnl-{0}.pdflist" -f [Guid]::NewGuid().ToString('N'))
    Set-Content -LiteralPath $listPath -Value $pdfPaths -Encoding UTF8

    Write-LauncherLog "Starting app with list file: $listPath"

    $process = Start-Process -FilePath $exePath -ArgumentList @('--open-from', $listPath) -PassThru -Wait
    $exitCode = if ($null -ne $process.ExitCode) { $process.ExitCode } else { 0 }

    Write-LauncherLog "App exited with code $exitCode"
    exit $exitCode
}
catch {
    Write-LauncherLog "Launcher error: $($_.Exception.Message)"
    exit 1
}
