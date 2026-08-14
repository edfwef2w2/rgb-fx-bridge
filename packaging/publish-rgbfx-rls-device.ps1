# Inject RgbFx into ROG Live Service deviceinfo.ini so AuraPlugin can fill
# GetDeviceStatusNew DisplayName/GUID for Extension_Card.
# Display name is the lighting host's hostname (display-name.txt / COMPUTERNAME).
# RLS may rewrite this file from the cloud — re-run after RLS updates.
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$Clsid = "B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73"
$Dir = "C:\ProgramData\RgbFx\AacHal"
New-Item -ItemType Directory -Force -Path $Dir | Out-Null
# 983040 = 0xF0000 Ext Header / Extension_Card
Set-Content -Path (Join-Path $Dir "type.txt") -Value "983040" -Encoding ASCII

$DisplayName = $env:RGBFX_DEVICE_NAME
if ([string]::IsNullOrWhiteSpace($DisplayName) -and (Test-Path (Join-Path $Dir "display-name.txt"))) {
    $DisplayName = (Get-Content -LiteralPath (Join-Path $Dir "display-name.txt") -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($DisplayName)) { $DisplayName = $env:COMPUTERNAME }
Set-Content -Path (Join-Path $Dir "display-name.txt") -Value $DisplayName -Encoding ASCII

$section = @"

[$DisplayName]
Name=$DisplayName
DisplayName=$DisplayName
DeviceType=Extension_Card
PrimitiveDeviceType=Extension_Card
LStype=Extension_Card
DeviceCount=1
LightingMode=SUPPORTAURA
PID=none
PIDMode=none
Mode=none
GUID=$Clsid
ErrorCode=0
SyncStatus=true
NeedRestart=false
Plugin=1
Firmware_Count=0
HAL_Count=1
HAL_regkey_1=$Clsid
HAL_regkeyname_1=Version
Parameters_Count=0
HTML_Count=0
SDK_Count=0
FirmwareFlow=0
SupportMatrix=0
MatrixUpdateStatus=0
DependentJsonVersion=0
StageRollOutSkipDownload=0
"@

$paths = @(
    "C:\ProgramData\ASUS\ROG Live Service\deviceinfo.ini",
    "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service\deviceinfo.ini"
)
foreach ($p in $paths) {
    if (-not (Test-Path -LiteralPath $p)) { continue }
    $c = Get-Content -LiteralPath $p -Raw
    $stripped = [regex]::Replace($c, '(?ms)^\[.*?\](?:(?!^\[).)*?' + [regex]::Escape($Clsid) + '(?:(?!^\[).)*', '')
    Copy-Item -LiteralPath $p -Destination ($p + ".bak-rgbfx") -Force
    Set-Content -LiteralPath $p -Value ($stripped.TrimEnd() + $section) -NoNewline
    Write-Host "deviceinfo DisplayName=$DisplayName -> $p"
}

Write-Host "type.txt=983040 (Extension_Card) DisplayName=$DisplayName"
Write-Host "Restart LightingService if it is already running, then reopen Armoury Crate."
Write-Host "  Restart-Service LightingService -Force"
