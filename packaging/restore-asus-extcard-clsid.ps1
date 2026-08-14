# Restore official ASUS Extension Card HAL CLSID after accidental RgbFx experiments.
# Does NOT install RgbFx. Safe to re-run. Requires Administrator.
#
# Only touches:
#   - WOW6432Node display name / AppID on ExtCard CLSID {662181CB-...85C7}
#   - experimental AppID {662181CB-...85C8}
# Never changes LocalServer32 paths or Category Instance metadata.

#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$clsid = "{662181CB-F1F8-4AD8-ABDD-3661A51A85C7}"
$appid = "{662181CB-F1F8-4AD8-ABDD-3661A51A85C8}"
$officialName = "ASUSAuraExtCardHal"

Write-Host "== Restore ASUS ExtCard CLSID =="

$wowPaths = @(
    "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\$clsid",
    "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$clsid"
)

foreach ($p in $wowPaths) {
    if (-not (Test-Path $p)) {
        Write-Host "skip missing $p"
        continue
    }
    $before = (Get-ItemProperty $p)."(default)"
    Set-ItemProperty -Path $p -Name "(default)" -Value $officialName
    Remove-ItemProperty -Path $p -Name "AppID" -ErrorAction SilentlyContinue
    Write-Host "WOW CLSID: '$before' -> '$((Get-ItemProperty $p).'(default)')' ($p)"
}

$appPaths = @(
    "HKLM:\SOFTWARE\Classes\AppID\$appid",
    "HKLM:\SOFTWARE\WOW6432Node\Classes\AppID\$appid",
    "HKLM:\SOFTWARE\Classes\WOW6432Node\AppID\$appid",
    "HKLM:\SOFTWARE\WOW6432Node\AppID\$appid"
)
foreach ($p in $appPaths) {
    if (Test-Path $p) {
        Remove-Item -Path $p -Recurse -Force
        Write-Host "removed $p"
    }
}

$x64 = "HKLM:\SOFTWARE\Classes\CLSID\$clsid"
if (Test-Path $x64) {
    Write-Host "x64 (default)=$((Get-ItemProperty $x64).'(default)')"
    Write-Host "x64 LocalServer32=$((Get-ItemProperty "$x64\LocalServer32").'(default)')"
}

Write-Host "Done. Restart LightingService if Aura still looks odd."
