$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionRoot = Resolve-Path (Join-Path $scriptDir "..")
$projectPath = Join-Path $solutionRoot "F1Telemetry.App\F1Telemetry.App.csproj"
$publishDir = Join-Path $solutionRoot "publish"
$mainExePath = Join-Path $publishDir "F1Telemetry.App.exe"

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
    /p:Version=1.5.0 `
    /p:AssemblyVersion=1.5.0.0 `
    /p:FileVersion=1.5.0.0 `
    /p:InformationalVersion=1.5.0 `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $mainExePath)) {
    throw "Expected main exe was not found: $mainExePath"
}

Write-Host "Publish directory: $publishDir"
Write-Host "Main exe: $mainExePath"
Write-Host "Next: use Inno Setup to compile build/F1Telemetry.iss"
