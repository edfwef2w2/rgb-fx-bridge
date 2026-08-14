# GetCapability format notes

## Status (2026-08-11)

Live `GetCapability` BSTR dump from MB HAL is **not yet captured**:

| Call | Result |
|------|--------|
| `CoCreate` MB HAL | OK |
| `QI IAsusAacLedDeviceHal2` | OK |
| `Enumerate2` | **hr=0, count=4**, but VARIANT body still empty (`vt=0`) with current marshal |
| `Enumerate` | `0x80004005` / `0x800706F4` — signature still wrong |

Until VARIANT/SAFEARRAY layout is fixed in `tools/AuraCapabilityDump`, `RgbFx.AacHal` uses a **heuristic text capability** from AuraSDK field-name order (`CapabilityBuilder`), overridable by:

```text
RGBFX_AAC_CAPABILITY_FILE=C:\path\to\live-capability.txt
```

## Known field names (AuraSDK / AacMB strings)

```
device, name, id, layout,
led_count, varied, max_led_count, max_total_led_count,
static_id, argb_id, size, width, height,
led_name, led_location_index,
supported_effect, effect, synchronizable, supported_standby_effect,
manufacturer, model, device_config, support_function, led_groups
```

## Device list (RogAura30 QueryAllDevice)

AddressableStrip examples: count=120, width=120, height=1, model=ARGB HEADER.

## Next dump step

Fix `Enumerate2` out-VARIANT (likely pre-allocated `SAFEARRAY` of `IUnknown` size=`count`, second call fills). Then save `artifacts/capability-dump/MB-*-capability.txt` and point `RGBFX_AAC_CAPABILITY_FILE` at it.
