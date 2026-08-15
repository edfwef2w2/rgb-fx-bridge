# Aura AAC HAL reverse notes (local machine)

## Strategy

- **Add** a new HAL CLSID; never hijack ExtCard/MB/DRAM.
- ExtCard residual `RgbFx Native Fake AAC HAL` was restored to `ASUSAuraExtCardHal`.

## Category registration

```
HKLM\SOFTWARE\Classes\CLSID\{9C9E903E-BBC7-4A0E-8326-ED6AC85B9FCC}\Instance\
  {E9BBD754-6CF4-492E-BA89-782177A2771B}\Instance\{HAL_CLSID}
```

RgbFx HAL CLSID: `{B7E8C2A1-4F3D-4E9A-9C1B-8D2E6F0A5B73}`

## COM interfaces (from Aac3572MbHal.tlb)

| Interface | IID | Notes |
|-----------|-----|--------|
| IAacLedDevice | `{61711778-…}` | GetCapability(BSTR*), SetEffect, Synchronize |
| IAsusAacLedDeviceHal | `{f2c8d5b4-…e3ba}` | Enumerate |
| IAsusMotherboardHal | extends Hal | Bios on/off ×4 |
| IAsusAacLedDeviceHal2 | `{816e764a-…}` | Enumerate2; **vtable = Hal + Bios + Enumerate2** |
| IAacLedDeviceHal | `{f2c8d5b4-…e3b9}` | DRAM: Enumerate + Enumerate2 |

## Effect IDs (RogAura30 GetDeviceCap.xml)

See [AURA_EFFECT_IDS.md](AURA_EFFECT_IDS.md).

## Scripts

| Script | Purpose |
|--------|---------|
| `packaging/restore-asus-extcard-clsid.ps1` | Undo ExtCard display-name pollution |
| `packaging/register-rgbfx-aac-hal.ps1` | Register **new** device only |
| `packaging/unregister-rgbfx-aac-hal.ps1` | Remove RgbFx only |
| `tools/AuraCapabilityDump` | Build host: dump + COM LocalServer (`--server`) |

## Build note

Sources live under `src/RgbFx.AacHal/`. **Build the runnable host via**:

```powershell
dotnet build tools/AuraCapabilityDump/AuraCapabilityDump.csproj -c Release
```

Do **not** put `[ComVisible(true)]` on server types on this machine — the resulting PE was deleted before copy (AV/heuristic). CCW still works without it for `ComImport` interface implementations.

Verified: `Activator.CreateInstance(CLSID)` → `System.__ComObject` after register.

## Env (RgbFx AAC HAL host)

| Variable | Meaning |
|----------|---------|
| `RGBFX_MSI_URL` | e.g. `http://192.168.x.x:17700` |
| `RGBFX_ZONES` | comma zones for frame map |
| `RGBFX_LED_COUNT` | virtual LED count (default 8) |
| `RGBFX_AAC_CAPABILITY_FILE` | override GetCapability body |
| `RGBFX_COLOR_ORDER` | `bgr` (default) or `rgb` |

Frame path: `SetEffect` keeps only the latest color array and returns; a worker POSTs `/api/v1/frame`. Server `apply_frame` uses `_write_state(fast=True)` (single HID packet). Aura sync budget is 64ms (`AuEngine`).

## Why Armoury may not show the device (2026-08-11)

### Confirmed working
| Step | Evidence |
|------|----------|
| 32-bit WOW category registration | `LightingService` PE is **x86**; category under `WOW6432Node` lists **RgbFx Bridge** |
| CreateHal | `LightingService.log`: `Target GUID => {B7E8C2A1-…}` |
| LocalServer start | `artifacts\aachal-x86*\AuraCapabilityDump.exe` process starts |
| Enumerate2 invoked | `aachal.log`: `Enumerate2 …` |

### Fixed (2026-08-11) — device recognized by LightingService
| Issue | Fix |
|-------|-----|
| **DoEnumerateDevices EXCEPTION - 01** after Enumerate2 | LS QIs **`IAacLedDeviceOpt2`** (`a146f057-…`) after unpack; implement full chain: `IAacLedDevice` + `Opt` + `VariedLedCount` + `IAacLedDevice2` + **`IAacLedDeviceOpt2`** |
| **GetCapability never called** | Resolved once Opt2 QI succeeds — LS then calls GetCapability, SetManualLedCount(120), SetEffect |
| Log proof | `Apply Success, ModelName = MSI Mystic Bridge`; `aachal.log` shows GetCapability / SetEffect |
| Shared **x86 .NET** not installed | Still need **self-contained win-x86** publish (`artifacts/aachal-x86-v8`) |

### UI still missing (Armoury UWP) — root cause (2026-08-11)

Armoury **does not** build the device list from `GetDeviceStatusNew.xml` alone.
AuraPlugin `GetDeviceStatusNew` merges:

1. **ROG Live Service** `SetDeviceInfo` models (primary): MB, GameFirst, FanXpert, **AddressableHeader (count=3, GUID=MB HAL)**, Memory  
2. **LightingService** type list: Mainboard_Master, GSkillDram, AddressableStrip  

Our RgbFx HAL:

| Layer | Status |
|-------|--------|
| LS CreateHal / Enumerate2 / GetCapability / SetEffect | **Works** |
| `QueryAllDevice.xml` → `MSI Mystic Bridge` | **Present** |
| RLS `SetDeviceInfo` with GUID `B7E8…` | **Never** |
| AddressableHeader `DeviceCount` | Stays **3** (MB headers only) |
| Aura Sync checkbox list | Only 3 groups — no separate RgbFx row |

File inject into `GetDeviceStatusNew.xml` is **wiped** when AuraPlugin refreshes from RLS+LS (~seconds).

### Type sweep (2026-08-15) — no CLSID hijack

`GetAllPossibleDeviceId` after capability `<type>` (fresh LS process):

| type | LS lightingname | In this PC's RLS? | Notes |
|---|---|---|---|
| 0x11000 | AddressableStrip | yes, ADD_HEADER=3 | never a new 奥创/Creator port |
| 0x20000 | Vga | no | extra id; GPU zone UI, not 120-LED strip |
| **0xA0000** | **HUE** | **no** | extra id; Aura Hue is per-light RGB |
| 0xF0000 | Extension_Card | no | extra id; Fan Extension Card II = one color |
| 0x80000 | (no new id in log) | — | did not register as Keyboard here |

| 0x80000 | (none) | — | HAL starts (`<type>524288`), **never** in `GetAllPossibleDeviceId` |

Aura UI completeness on this PC:

| | Keyboard | Vga | HUE |
|---|---|---|---|
| LS standalone id | no | **yes** | yes |
| Official plugin schema | per-key editor (only if a keyboard exists) | native GPU zones (`VGAL`/`VGAH`) | **`<device key="HUE"><last_ip/></device>`** — Hue bridge only |
| 120-LED strip editor | n/a | no | no |
| Hijack official CLSID | no | no | no |

HUE is incomplete for a fake AAC device. Keyboard never lists when the HAL enumerates five 120×1 strips.

### Keyboard retry (2026-08-15)

Product path is **one 20×6 Keyboard** (`<type>524288</type>`, `varied=0`, `led_location_index` 0–119), not five strips. Category DeviceType / RLS `DeviceType` = `Keyboard`. `SetEffect` 120 colors are sliced across live `/api/v1/zones` (hardware is still per-zone). Do not restart RLS.

Live result after `aachal-x86-v22`:

| Check | Result |
|---|---|
| CreateHal `{B7E8…}` | yes |
| Enumerate2 | **1** device, HAVEIID = `IAacLedDevice` |
| `QueryAllDevice` | **Keyboard** 20×6 / 120, model = live identity |
| `GetAllPossibleDeviceId` | still MB / DRAM / AddressableStrip only |
| LS controllers | sync=3, **non-sync=1** (this Keyboard) |
| `GetDeviceStatusNew` | Keyboard row, `DeviceSync=0`, `StatusReady=0`, empty DisplayName/GUID |
| `GetDeviceCap` `<device key="Keyboard">` | stub `<other><count key="0"/>` |
| After GetCapability QI | `04df988d-…` / `5fa4dac9-…` (unimplemented) |

### Official AddressableStrip group (2026-08-15)

Product path is **merge into `[AddressableHeader]`**, not a second RLS device.

| Source | What it counts |
|---|---|
| MB HAL Enumerate | Official ARGB ports only (this board: 3) |
| `AACAddressableStrip::setUIKey` `NumberOfHals` / `ADD_HEADERs` | Same official port count |
| `QueryAllDevice` `model=ARGB HEADER` | Same official port count (ignore our `hostname-board-zone` models) |
| RLS `[AddressableHeader] DeviceCount` | What Creator Rescan expands; Plugin may cache until Armoury backends bounce |
| LS `connecteddevice AddressableStrip` | Official + our HAL (e.g. 8) — Plugin does **not** use this for the checkbox `DeviceCount` |

Install detects official ports from `QueryAllDevice` ARGB HEADER (fallback: LS log `NumberOfHals`, `deviceinfo.ini.bak-rgbfx`). Persists `official-header-count.txt`. Expand `DeviceCount = official + zones`. Uninstall restores **that** official count, never a hardcoded 3.

Capability defaults: `argb_id=3`, `led_count=120`, model `MSI Mystic Bridge`.

### Independent device (2026-08-14)

Capability `<type>` maps to LS `lightingname` (verified by sweep):

| type | QueryAllDevice type | lightingname |
|------|---------------------|--------------|
| 0x11000 (69632) | AddressableStrip | AddressableStrip |
| 0x12000 | Desktop | Desktop |
| 0x13000 | NOTEBOOK_CHASSIS | NOTEBOOK_CHASSIS |
| 0x20000 | VGA card | Vga |
| 0x70000 | DIMM | GSkillDram |
| 0x80000 | Keyboard | Keyboard |
| 0xA0000 | HUE | HUE |
| **0xF0000 (983040)** | **Ext Header** | **Extension_Card** |

Default HAL type is now **0xF0000**. LS then:

- `connecteddevice key="Extension_Card"`
- `LS Provide TYPE Count : 4` including `Extension_Card (512)`
- `GetDeviceStatusNew.xml` **DeviceCount=4** with a 4th `<Name DeviceType="Extension_Card">`

AuraPlugin still logs: `Extension_Card can't be found in the devinfo provided by RLS, push back LS default` — DisplayName/GUID stay empty until RLS `deviceinfo.ini` has a matching section. Inject:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/publish-rgbfx-rls-device.ps1
Restart-Service LightingService -Force
```

Do **not** restart `ROG Live Service` after inject — it regenerates `deviceinfo.ini` from the cloud and drops the section.

Override type without reboot: `%ProgramData%\RgbFx\AacHal\type.txt` (decimal).

### DeviceType note
Category `DeviceType` must be a **HAL kind** (`Extension Card`, `DRAM`, `Motherboard`, …), **not** `AddressableStrip` (that is a *device* under motherboard HAL). Use `Pluging=1` to match real ExtCard.

### Workarounds / env
| Variable | Meaning |
|----------|---------|
| `RGBFX_ENUM2_MODE=arrayiid` | **Default** — SAFEARRAY VT_UNKNOWN + HAVEIID (VariedLedCount) |
| `RGBFX_ENUM2_MODE=vector` | SAFEARRAY without HAVEIID |
| `RGBFX_ENUM2_MODE=single` | single VT_UNKNOWN |
| `RGBFX_ENUM2_MODE=vararray` | VT_ARRAY\|VT_VARIANT |
| `RGBFX_ENUM2_MODE=empty` | count=0 — no device |
| `RGBFX_MSI_URL` | e.g. `http://127.0.0.1:17700` for `/api/v1/frame` (Machine env if LS launches host) |

## Quick start

```powershell
# 1) restore any ExtCard pollution
powershell -ExecutionPolicy Bypass -File packaging/restore-asus-extcard-clsid.ps1

# 2) publish 32-bit self-contained host + register
dotnet publish tools/AuraCapabilityDump/AuraCapabilityDump.csproj -c Release -r win-x86 --self-contained true -o artifacts/aachal-x86
powershell -ExecutionPolicy Bypass -File packaging/register-rgbfx-aac-hal.ps1

# 3) point at MSI host (machine env if LS launches the server)
setx RGBFX_MSI_URL "http://192.168.x.x:17700" /M

# 4) restart Aura stack
Restart-Service LightingService -Force
# Check: %ProgramData%\RgbFx\AacHal\aachal.log
# Check: %ProgramData%\ASUS\ARMOURY CRATE Diagnosis\LightingService\LightingService.log
```
