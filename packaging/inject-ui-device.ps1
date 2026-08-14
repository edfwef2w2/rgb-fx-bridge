# Inject RgbFx into Armoury UWP device list files (GetDeviceStatusNew + Plugin_Status).
# LightingService rewrites these on restart — re-run after LS restart if entry disappears.
#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

$statusNew = "C:\ProgramData\ASUS\RogAura30\GetDeviceStatusNew.xml"
$plugin = "C:\ProgramData\ASUS\RogAura30\Plugin_Status.ini"
$clsid = "B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73"
$nameFile = "C:\ProgramData\RgbFx\AacHal\display-name.txt"
$display = $env:COMPUTERNAME
if (Test-Path -LiteralPath $nameFile) {
    $n = (Get-Content -LiteralPath $nameFile -Raw).Trim()
    if (-not [string]::IsNullOrWhiteSpace($n)) { $display = $n }
}

if (-not (Test-Path -LiteralPath $statusNew)) {
    throw "Missing $statusNew — is Armoury / LightingService installed?"
}

Copy-Item -LiteralPath $statusNew -Destination ($statusNew + ".bak-rgbfx") -Force

# If already injected, leave structure but refresh our block
[xml]$doc = Get-Content -LiteralPath $statusNew -Raw
$names = @($doc.root.Name)
$ours = $names | Where-Object { $_.DeviceID -eq $clsid }
if ($ours) {
    Write-Host "Already present in GetDeviceStatusNew (DeviceID=$clsid)"
} else {
    $mb = $names | Where-Object { $_.DeviceType -eq "Mainboard_Master" } | Select-Object -First 1
    $mem = $names | Where-Object { $_.DeviceType -eq "GSkillDram" } | Select-Object -First 1
    $strip = $names | Where-Object { $_.DeviceType -eq "AddressableStrip" } | Select-Object -First 1

    $xml = @"
<root>
    <DeviceCount>4</DeviceCount>
    <HALIsInstalled>1</HALIsInstalled>
$($mb.OuterXml)
$($mem.OuterXml)
$($strip.OuterXml)
    <Name UWPDisplayName="$display" DeviceType="AddressableStrip">
        <DisplayNameMultiLang/>
        <Type>4096</Type>
        <PrimitiveDeviceTypeID/>
        <DeviceID>$clsid</DeviceID>
        <DeviceInGame>0</DeviceInGame>
        <DeviceSync>1</DeviceSync>
        <ACControllable>1</ACControllable>
        <CheckBoxEnable>1</CheckBoxEnable>
        <DependentAppStatus>0</DependentAppStatus>
        <DialogString/>
        <InfoIconEnable>0</InfoIconEnable>
        <InfoString/>
        <LightingModeEnable>0</LightingModeEnable>
        <DeviceCount>1</DeviceCount>
        <DeviceIndex>0</DeviceIndex>
        <PIDMode>none</PIDMode>
        <PartNumber_90/>
        <StatusReady>1</StatusReady>
    </Name>
</root>
"@
    Set-Content -LiteralPath $statusNew -Value $xml -Encoding UTF8
    Write-Host "Wrote GetDeviceStatusNew.xml with $display"
}

if (Test-Path -LiteralPath $plugin) {
    Copy-Item -LiteralPath $plugin -Destination ($plugin + ".bak-rgbfx") -Force
    $c = Get-Content -LiteralPath $plugin -Raw
    if ($c -notmatch [regex]::Escape($clsid)) {
        $c = $c -replace 'DeviceName=Mainboard_Master, GSkillDram, AddressableStrip, ',
            'DeviceName=Mainboard_Master, GSkillDram, AddressableStrip, AddressableStrip, '
        $c = $c -replace 'DeviceModelName=ROG MAXIMUS Z790 APEX ENCORE, Memory, Addressable LED Strip, ',
            "DeviceModelName=ROG MAXIMUS Z790 APEX ENCORE, Memory, Addressable LED Strip, $display, "
        $c = $c -replace 'DeviceSync=1, 1, 1, ', 'DeviceSync=1, 1, 1, 1, '
        $c = $c -replace 'DeviceType=16, 2048, 4096, ', 'DeviceType=16, 2048, 4096, 4096, '
        $c = $c -replace 'DeviceCheckBoxEnable=2, 1, 1, ', 'DeviceCheckBoxEnable=2, 1, 1, 1, '
        $c = $c -replace 'DependentAppStatus=0, 0, 0, ', 'DependentAppStatus=0, 0, 0, 0, '
        $c = $c -replace 'DeviceLigintModeEnable=0, 0, 0, ', 'DeviceLigintModeEnable=0, 0, 0, 0, '
        $c = $c -replace 'DeviceInfoIconEnable=0, 0, 0, ', 'DeviceInfoIconEnable=0, 0, 0, 0, '
        $c = $c -replace 'DeviceInfoString=, , , ', 'DeviceInfoString=, , , , '
        $c = $c -replace 'DeviceID=E7C8DA76-C9B9-4297-8681-DD878330AFE7, 05B8E7A3-7D5D-41DE-B6CC-5E6205837FB7, E7C8DA76-C9B9-4297-8681-DD878330AFE7, ',
            "DeviceID=E7C8DA76-C9B9-4297-8681-DD878330AFE7, 05B8E7A3-7D5D-41DE-B6CC-5E6205837FB7, E7C8DA76-C9B9-4297-8681-DD878330AFE7, $clsid, "
        $c = $c -replace 'DeviceInfonum=0, 0, 0, ', 'DeviceInfonum=0, 0, 0, 0, '
        $c = $c -replace 'DeviceLEDOnOff=1, 1, 1, ', 'DeviceLEDOnOff=1, 1, 1, 1, '
        $c = $c -replace 'DeviceChatmode=0, 0, 0, ', 'DeviceChatmode=0, 0, 0, 0, '
        $c = $c -replace 'DeviceDialogString=CantUnsyncHint, , , ', 'DeviceDialogString=CantUnsyncHint, , , , '
        $c = $c -replace 'DeviceACcontrol=1, 1, 1, ', 'DeviceACcontrol=1, 1, 1, 1, '
        $legacy = [regex]::Escape($display)
        if ($c -notmatch "AddressableStrip/$legacy") {
            $c = $c -replace 'AddressableStrip/Addressable LED Strip=Checked',
                "AddressableStrip/Addressable LED Strip=Checked`r`nAddressableStrip/$display=Checked"
        }
        Set-Content -LiteralPath $plugin -Value $c -Encoding UTF8 -NoNewline
        Write-Host "Patched Plugin_Status.ini"
    } else {
        Write-Host "Plugin_Status already has $clsid"
    }
}

Write-Host ""
Write-Host "Next:"
Write-Host "  1) Fully exit Armoury Crate (tray + UWP)"
Write-Host "  2) Reopen Armoury Crate -> Aura Sync / device list"
Write-Host "  3) Look for: $display"
Write-Host "Note: Restart-Service LightingService will regenerate these files and drop the inject."
Write-Host "      Re-run this script after LS restarts."
