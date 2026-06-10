<#
.SYNOPSIS
    Builds and publishes OpenSourceTree for Windows (or any RID you pass).

.EXAMPLE
    .\build.ps1                      # self-contained Release build for win-x64 -> dist\win-x64
    .\build.ps1 -Runtime win-arm64   # Windows on ARM
    .\build.ps1 -BuildOnly           # plain Debug build, no publish
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$BuildOnly,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\OpenSourceTree\OpenSourceTree.csproj"

if ($BuildOnly) {
    dotnet build $project -c $Configuration
    exit $LASTEXITCODE
}

$out = Join-Path $root "dist\$Runtime"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

Write-Host "Publishing $Configuration / $Runtime (self-contained: $selfContained) -> $out" -ForegroundColor Cyan
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContained `
    -p:PublishReadyToRun=false `
    -o $out

if ($LASTEXITCODE -eq 0) {
    Write-Host "Done. Run: $out\OpenSourceTree.exe" -ForegroundColor Green
}
exit $LASTEXITCODE
