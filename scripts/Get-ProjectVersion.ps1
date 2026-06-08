function Get-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $buildProps = Join-Path $ProjectRoot "Directory.Build.props"
    if (Test-Path $buildProps) {
        [xml]$buildPropsXml = Get-Content $buildProps
        $version = ($buildPropsXml.Project.PropertyGroup |
            Where-Object { $_.Version } |
            Select-Object -First 1).Version
        if ($version) {
            return $version
        }
    }

    $project = Join-Path $ProjectRoot "src\DeliveryNoteLabeler\DeliveryNoteLabeler.csproj"
    if (Test-Path $project) {
        [xml]$projectXml = Get-Content $project
        $version = ($projectXml.Project.PropertyGroup |
            Where-Object { $_.Version } |
            Select-Object -First 1).Version
        if ($version) {
            return $version
        }
    }

    throw "Could not determine app version from Directory.Build.props or DeliveryNoteLabeler.csproj"
}
