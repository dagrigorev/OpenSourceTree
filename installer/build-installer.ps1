<#
.SYNOPSIS
    Builds the OpenSourceTree Windows installer (MSI) with WiX v5.

.DESCRIPTION
    1. Publishes a self-contained Release build for the chosen runtime (unless -SkipPublish).
    2. Compiles installer\Product.wxs into dist\OpenSourceTree-<version>-<rid>.msi.

    Requires the WiX v5 dotnet tool and the UI extension:
        dotnet tool install --global wix --version 5.0.2
        wix extension add -g WixToolset.UI.wixext/5.0.2

.EXAMPLE
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -Runtime win-arm64 -SkipPublish
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root "dist\$Runtime"
$outMsi = Join-Path $root "dist\OpenSourceTree-$Version-$Runtime.msi"

# Make the WiX global tool reachable in a fresh shell.
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

if (-not $SkipPublish) {
    Write-Host "Publishing self-contained $Runtime build..." -ForegroundColor Cyan
    & (Join-Path $root "build.ps1") -Runtime $Runtime
}

if (-not (Test-Path (Join-Path $publishDir "OpenSourceTree.exe"))) {
    throw "Publish output not found at $publishDir. Run without -SkipPublish first."
}

Write-Host "Building installer -> $outMsi" -ForegroundColor Cyan
wix build (Join-Path $PSScriptRoot "Product.wxs") `
    -arch x64 `
    -ext WixToolset.UI.wixext `
    -d "PublishDir=$publishDir" `
    -d "ProjectDir=$root" `
    -o $outMsi

if ($LASTEXITCODE -eq 0) {
    $sizeMb = [math]::Round((Get-Item $outMsi).Length / 1MB, 1)
    Write-Host "Done: $outMsi ($sizeMb MB)" -ForegroundColor Green
}
exit $LASTEXITCODE
