#Requires -RunAsAdministrator
param(
    [string]$SysPath = "",
    [string]$InfPath = ""
)

$ErrorActionPreference = "Stop"
Write-Host "RgbFx Bridge install (test-signing path)"
Write-Host "1) Enable test signing if needed: bcdedit /set testsigning on  (reboot)"
Write-Host "2) Install driver INF with pnputil / devcon when .sys is built"
Write-Host "3) Run RgbFx.UI to configure remote MSI target (port 17700 /api/v1)"
Write-Host "4) SourceMode=pipe after driver is live; SourceMode=simulator to test API only"
Write-Host ""
Write-Host "Config path: $env:ProgramData\RgbFxBridge\config.json"
Write-Host "Pipe name:   \\.\pipe\RgbFxLampArray"
Write-Host ""
Write-Host "See docs/INSTALL_TESTSIGN.md for full steps."

if ($InfPath -and (Test-Path $InfPath)) {
    pnputil /add-driver $InfPath /install
}
