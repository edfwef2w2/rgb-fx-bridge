# Register RgbFx as a NEW Aura AAC HAL (never touches ASUS ExtCard/MB CLSIDs).
# LightingService.exe is 32-bit — must register WOW6432Node CLSID + category too.
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$Clsid = "{B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73}"
$CategoryRoot = "{9C9E903E-BBC7-4A0E-8326-ED6AC85B9FCC}"
$CategoryInst = "{E9BBD754-6CF4-492E-BA89-782177A2771B}"
$ProgId = "RgbFx.AacHal.1"
$ProgIdVi = "RgbFx.AacHal"
# Category DeviceType (HAL kind). Override with RGBFX_HAL_DEVICE_TYPE.
# Note: AddressableStrip *devices* from a non-MB HAL still get LS Apply, but
# Armoury UI lists only RLS SetDeviceInfo groups (MB / Memory / AddressableHeader).
# Safe options: Extension Card | Motherboard | DRAM | AmbientDevice
$DeviceType = $env:RGBFX_HAL_DEVICE_TYPE
if ([string]::IsNullOrWhiteSpace($DeviceType)) { $DeviceType = "Extension Card" }

function Get-BridgeDisplayName {
    $nameFile = "C:\ProgramData\RgbFx\AacHal\display-name.txt"
    if (Test-Path -LiteralPath $nameFile) {
        $n = (Get-Content -LiteralPath $nameFile -Raw).Trim()
        if (-not [string]::IsNullOrWhiteSpace($n)) { return $n }
    }
    if (-not [string]::IsNullOrWhiteSpace($env:RGBFX_DEVICE_NAME)) {
        return $env:RGBFX_DEVICE_NAME.Trim()
    }
    return $env:COMPUTERNAME
}

$DisplayName = Get-BridgeDisplayName
New-Item -ItemType Directory -Force -Path "C:\ProgramData\RgbFx\AacHal" | Out-Null
Set-Content -Path "C:\ProgramData\RgbFx\AacHal\display-name.txt" -Value $DisplayName -Encoding ASCII
Set-Content -Path "C:\ProgramData\RgbFx\AacHal\type.txt" -Value "983040" -Encoding ASCII
Write-Host "DisplayName=$DisplayName"

$repoRoot = Split-Path -Parent $PSScriptRoot
# LightingService is 32-bit. Prefer latest self-contained win-x86 publish (no shared x86 .NET required).
$candidates = @(
    (Join-Path $repoRoot "artifacts\aachal-x86-v9"),
    (Join-Path $repoRoot "artifacts\aachal-x86-v8"),
    (Join-Path $repoRoot "artifacts\aachal-x86-v7"),
    (Join-Path $repoRoot "artifacts\aachal-x86")
)
$pubDir = $null
$exe = $null
foreach ($c in $candidates) {
    $tryExe = Join-Path $c "AuraCapabilityDump.exe"
    if (Test-Path -LiteralPath $tryExe) {
        $pubDir = $c
        $exe = $tryExe
        break
    }
}
if (-not $exe) {
    $pubDir = Join-Path $repoRoot "artifacts\aachal-x86"
    Write-Host "Publishing self-contained win-x86 host..."
    $proj = Join-Path $repoRoot "tools\AuraCapabilityDump\AuraCapabilityDump.csproj"
    New-Item -ItemType Directory -Force -Path $pubDir | Out-Null
    dotnet publish $proj -c Release -r win-x86 --self-contained true -o $pubDir
    $exe = Join-Path $pubDir "AuraCapabilityDump.exe"
}
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Missing $exe — win-x86 publish failed."
}
$exe = (Resolve-Path -LiteralPath $exe).Path
$localServer = "`"$exe`" --server"
Write-Host "LocalServer32 = $localServer"
Write-Host "PubDir = $pubDir"

# Machine-level env for LocalServer launched by LightingService (service session)
$enumMode = $env:RGBFX_ENUM2_MODE
if ([string]::IsNullOrWhiteSpace($enumMode)) { $enumMode = "arrayiid" }
[Environment]::SetEnvironmentVariable("RGBFX_ENUM2_MODE", $enumMode, "Machine")
Write-Host "RGBFX_ENUM2_MODE (Machine) = $enumMode"

function Register-ClsidTree {
    param([string]$ClsidRoot)
    New-Item -Path $ClsidRoot -Force | Out-Null
    Set-ItemProperty -Path $ClsidRoot -Name "(default)" -Value "RgbFx AAC HAL"
    New-Item -Path "$ClsidRoot\LocalServer32" -Force | Out-Null
    Set-ItemProperty -Path "$ClsidRoot\LocalServer32" -Name "(default)" -Value $localServer
    New-Item -Path "$ClsidRoot\ProgID" -Force | Out-Null
    Set-ItemProperty -Path "$ClsidRoot\ProgID" -Name "(default)" -Value $ProgId
    New-Item -Path "$ClsidRoot\VersionIndependentProgID" -Force | Out-Null
    Set-ItemProperty -Path "$ClsidRoot\VersionIndependentProgID" -Name "(default)" -Value $ProgIdVi
    # Borrow ASUS MB typelib so IAsusAacLedDeviceHal2 / IAacLedDevice marshal via oleaut PS
    New-Item -Path "$ClsidRoot\TypeLib" -Force | Out-Null
    Set-ItemProperty -Path "$ClsidRoot\TypeLib" -Name "(default)" -Value "{57E4E792-2CF6-48E5-BF2B-10F19F857B9E}"
    New-Item -Path "$ClsidRoot\Version" -Force | Out-Null
    Set-ItemProperty -Path "$ClsidRoot\Version" -Name "(default)" -Value "1.0"
    Write-Host "CLSID ok: $ClsidRoot"
}

function Register-CategoryInstance {
    param([string]$InstPath)
    New-Item -Path $InstPath -Force | Out-Null
    Set-ItemProperty -Path $InstPath -Name "Name" -Value $DisplayName
    Set-ItemProperty -Path $InstPath -Name "Description" -Value "RgbFx lighting bridge"
    Set-ItemProperty -Path $InstPath -Name "Manufacturer" -Value "ASUSTeK COMPUTER INC."
    Set-ItemProperty -Path $InstPath -Name "DeviceModel" -Value $DisplayName
    Set-ItemProperty -Path $InstPath -Name "DeviceType" -Value $DeviceType
    Set-ItemProperty -Path $InstPath -Name "Version" -Value "0.1.0"
    Set-ItemProperty -Path $InstPath -Name "SpecVersion" -Value "1.0.0"
    # Motherboard Pluging=2; ExtCard=1. Match DeviceType.
    $plug = if ($DeviceType -match 'Motherboard|Ambient') { 2 } else { 1 }
    New-ItemProperty -Path $InstPath -Name "Pluging" -PropertyType DWord -Value $plug -Force | Out-Null
    Write-Host "Category ok: $InstPath (DeviceType=$DeviceType Pluging=$plug)"
}

# --- 64-bit view (native) ---
Register-ClsidTree "HKLM:\SOFTWARE\Classes\CLSID\$Clsid"
Register-CategoryInstance "HKLM:\SOFTWARE\Classes\CLSID\$CategoryRoot\Instance\$CategoryInst\Instance\$Clsid"

# --- 32-bit view (LightingService is x86 — THIS is the critical path) ---
# Both registry locations are used by different WOW layouts
foreach ($wowClsid in @(
        "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$Clsid",
        "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$Clsid"
    )) {
    Register-ClsidTree $wowClsid
}
foreach ($wowInst in @(
        "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$CategoryRoot\Instance\$CategoryInst\Instance\$Clsid",
        "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$CategoryRoot\Instance\$CategoryInst\Instance\$Clsid"
    )) {
    Register-CategoryInstance $wowInst
}

# ProgID (shared)
foreach ($pidKey in @("HKLM:\SOFTWARE\Classes\$ProgId", "HKLM:\SOFTWARE\Classes\WOW6432Node\$ProgId")) {
    New-Item -Path $pidKey -Force | Out-Null
    Set-ItemProperty -Path $pidKey -Name "(default)" -Value "RgbFx AAC HAL"
    New-Item -Path "$pidKey\CLSID" -Force | Out-Null
    Set-ItemProperty -Path "$pidKey\CLSID" -Name "(default)" -Value $Clsid
}
$pidVi = "HKLM:\SOFTWARE\Classes\$ProgIdVi"
New-Item -Path $pidVi -Force | Out-Null
Set-ItemProperty -Path $pidVi -Name "(default)" -Value "RgbFx AAC HAL"
New-Item -Path "$pidVi\CLSID" -Force | Out-Null
Set-ItemProperty -Path "$pidVi\CLSID" -Name "(default)" -Value $Clsid
New-Item -Path "$pidVi\CurVer" -Force | Out-Null
Set-ItemProperty -Path "$pidVi\CurVer" -Name "(default)" -Value $ProgId

Write-Host ""
Write-Host "DeviceType=$DeviceType (HAL kind for Aura category)"

# Publish display name into RLS deviceinfo so Armoury does not fall back to the
# Extension_Card product string. Do not restart ROG Live Service (it rewrites this file).
$clsidBare = $Clsid.Trim("{}")
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
GUID=$clsidBare
ErrorCode=0
SyncStatus=true
NeedRestart=false
Plugin=1
Firmware_Count=0
HAL_Count=1
HAL_regkey_1=$clsidBare
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

$iniPaths = @(
    "C:\ProgramData\ASUS\ROG Live Service\deviceinfo.ini",
    "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service\deviceinfo.ini"
)
foreach ($ini in $iniPaths) {
    if (-not (Test-Path -LiteralPath $ini)) { continue }
    try {
        $raw = Get-Content -LiteralPath $ini -Raw
        $stripped = [regex]::Replace($raw, '(?ms)^\[.*?\](?:(?!^\[).)*?' + [regex]::Escape($clsidBare) + '(?:(?!^\[).)*', '')
        if ($stripped -ne $raw) {
            Copy-Item -LiteralPath $ini -Destination ($ini + ".bak-rgbfx") -Force
        }
        Set-Content -LiteralPath $ini -Value ($stripped.TrimEnd() + $section) -NoNewline
        Write-Host "deviceinfo DisplayName=$DisplayName -> $ini"
    } catch {
        Write-Host "skip deviceinfo $ini : $($_.Exception.Message)"
    }
}

# Always bounce LightingService so it re-enumerates this HAL. Do not restart ROG Live Service.
try {
    Restart-Service -Name LightingService -Force -ErrorAction Stop
    Write-Host "LightingService restarted."
} catch {
    Write-Host "LightingService restart failed: $($_.Exception.Message)"
}

Write-Host "Done."
