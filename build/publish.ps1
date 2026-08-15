param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Resolve-Path (Join-Path $scriptDir "..")
$projectPath = Join-Path $solutionRoot "F1Telemetry.App\F1Telemetry.App.csproj"
$propsPath = Join-Path $solutionRoot "Directory.Build.props"
$publishDir = Join-Path $solutionRoot "publish"
$mainExePath = Join-Path $publishDir "F1Telemetry.App.exe"

[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$baseVersion = [string]$props.Project.PropertyGroup.VersionPrefix
$publishVersion = if ([string]::IsNullOrWhiteSpace($Version)) { $baseVersion } else { $Version }

if ($publishVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "Publish version must contain three or four numeric segments: $publishVersion"
}

$versionSegments = $publishVersion.Split('.')
foreach ($segment in $versionSegments) {
    $numericSegment = 0
    if (-not [int]::TryParse($segment, [ref]$numericSegment) -or $numericSegment -gt 65535) {
        throw "Publish version segments must be between 0 and 65535: $publishVersion"
    }
}

$assemblyVersion = if ($versionSegments.Count -eq 3) { "$publishVersion.0" } else { $publishVersion }

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -m:1 `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:Version=$publishVersion `
    /p:AssemblyVersion=$assemblyVersion `
    /p:FileVersion=$assemblyVersion `
    /p:InformationalVersion=$publishVersion `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $mainExePath)) {
    throw "Expected main exe was not found: $mainExePath"
}

Write-Host "Publish directory: $publishDir"
Write-Host "Main exe: $mainExePath"
Write-Host "Publish version: $publishVersion"
Write-Host "Next: use Inno Setup to compile build/F1Telemetry.iss"
