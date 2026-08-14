# Remove ONLY RgbFx AAC HAL registration (x64 + WOW). Never touches ASUS keys.
#Requires -RunAsAdministrator
$ErrorActionPreference = "Continue"

$Clsid = "{B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73}"
$CategoryRoot = "{9C9E903E-BBC7-4A0E-8326-ED6AC85B9FCC}"
$CategoryInst = "{E9BBD754-6CF4-492E-BA89-782177A2771B}"
$ProgId = "RgbFx.AacHal.1"
$ProgIdVi = "RgbFx.AacHal"

$paths = @(
    "HKLM:\SOFTWARE\Classes\CLSID\$CategoryRoot\Instance\$CategoryInst\Instance\$Clsid",
    "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$CategoryRoot\Instance\$CategoryInst\Instance\$Clsid",
    "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$CategoryRoot\Instance\$CategoryInst\Instance\$Clsid",
    "HKLM:\SOFTWARE\Classes\CLSID\$Clsid",
    "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$Clsid",
    "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$Clsid",
    "HKLM:\SOFTWARE\Classes\$ProgId",
    "HKLM:\SOFTWARE\Classes\WOW6432Node\$ProgId",
    "HKLM:\SOFTWARE\Classes\$ProgIdVi"
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        Remove-Item -Path $p -Recurse -Force
        Write-Host "removed $p"
    } else {
        Write-Host "skip $p"
    }
}

Get-Process AuraCapabilityDump -ErrorAction SilentlyContinue | Stop-Process -Force

$iniPaths = @(
    "C:\ProgramData\ASUS\ROG Live Service\deviceinfo.ini",
    "C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service\deviceinfo.ini"
)
$clsidBare = $Clsid.Trim("{}")
foreach ($ini in $iniPaths) {
    if (-not (Test-Path -LiteralPath $ini)) { continue }
    try {
        $raw = Get-Content -LiteralPath $ini -Raw
        $next = [regex]::Replace($raw, '(?ms)^\[.*?\](?:(?!^\[).)*?' + [regex]::Escape($clsidBare) + '(?:(?!^\[).)*', '')
        if ($next -ne $raw) {
            Set-Content -LiteralPath $ini -Value $next -NoNewline
            Write-Host "removed HAL section from $ini"
        }
    } catch {
        Write-Host "skip deviceinfo $ini : $($_.Exception.Message)"
    }
}

# Always bounce LightingService so it drops the cached HAL. Do not restart ROG Live Service.
try {
    Restart-Service -Name LightingService -Force -ErrorAction Stop
    Write-Host "LightingService restarted."
} catch {
    Write-Host "LightingService restart failed: $($_.Exception.Message)"
}

Write-Host "Unregistered RgbFx AAC HAL."
