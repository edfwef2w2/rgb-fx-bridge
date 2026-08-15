# Force-start Aura Creator and clear ASUS Update Center "NextVersion"
# so it does not show GoToACUpdateCenter (Armoury Crate has a new version).
#Requires -Version 5.1
$ErrorActionPreference = "Continue"

$UpdateRoot = "HKLM:\SOFTWARE\WOW6432Node\ASUS\Update"

function Test-Admin {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host "Need administrator to clear Update Center NextVersion..."
    $self = $MyInvocation.MyCommand.Path
    Start-Process -FilePath "powershell.exe" -Verb runas -Wait `
        -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$self`""
    exit $LASTEXITCODE
}

Write-Host "Clearing ASUS Update Center pending updates..."
if (Test-Path $UpdateRoot) {
    try {
        Set-ItemProperty -Path $UpdateRoot -Name "UpdateAvailable" -Value 0 -Type DWord -ErrorAction SilentlyContinue
    } catch { Write-Host $_ }
}

$clients = Join-Path $UpdateRoot "Clients"
if (Test-Path $clients) {
    Get-ChildItem $clients | ForEach-Object {
        $p = Get-ItemProperty $_.PSPath
        if ($p.NextVersion) {
            Write-Host "  $($p.name): pv=$($p.pv) -> $($p.NextVersion) (drop NextVersion)"
            Set-ItemProperty -Path $_.PSPath -Name "pv" -Value $p.NextVersion
            Remove-ItemProperty -Path $_.PSPath -Name "NextVersion" -ErrorAction SilentlyContinue
        }
    }
}

$states = Join-Path $UpdateRoot "ClientState"
if (Test-Path $states) {
    Get-ChildItem $states | ForEach-Object {
        $p = Get-ItemProperty $_.PSPath
        if ($p.UpdateAvailableCount -and [int]$p.UpdateAvailableCount -gt 0) {
            Write-Host "  ClientState $($_.PSChildName) UpdateAvailableCount=$($p.UpdateAvailableCount) -> 0"
            Set-ItemProperty -Path $_.PSPath -Name "UpdateAvailableCount" -Value 0 -Type DWord
            Remove-ItemProperty -Path $_.PSPath -Name "UpdateAvailableSince" -ErrorAction SilentlyContinue
        }
    }
}

Get-Process SetupAURACreator -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process AuraEditor -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$aumid = "B9ECED6F.AURACreator_qmba6cd70vzyy!App"
$fromStart = Get-StartApps | Where-Object { $_.Name -match "Aura Creator" } | Select-Object -First 1
if ($fromStart -and $fromStart.AppID) { $aumid = $fromStart.AppID }

Write-Host "Launching $aumid ..."
Start-Process -FilePath "explorer.exe" -ArgumentList "shell:AppsFolder\$aumid"
Start-Sleep -Seconds 3
if (Get-Process AuraEditor -ErrorAction SilentlyContinue) {
    Write-Host "Aura Creator started."
    exit 0
}

Write-Host "Aura Creator did not stay running."
exit 1
