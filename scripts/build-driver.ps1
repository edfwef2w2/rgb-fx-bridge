#Requires -Version 5.1
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\RgbFx.Driver\RgbFx.Driver.vcxproj"

if (-not (Test-Path $proj)) {
    throw "Driver project not found: $proj"
}

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
    2>$null | Select-Object -First 1

if (-not $msbuild) {
    Write-Host "MSBuild / WDK not found. Install VS 2022 + Windows Driver Kit."
    Write-Host "Managed projects can still build with: dotnet build src/RgbFxBridge.sln"
    exit 2
}

Write-Host "Building driver with $msbuild ..."
& $msbuild $proj /p:Configuration=$Configuration /p:Platform=$Platform /m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Driver build finished."
