# Architecture

```
Windows 11 Dynamic Lighting / Apps (LampArray API)
        │
        ▼
RgbFx.Driver (KMDF + VHF)  — virtual Chassis HID LampArray
        │  raw reports
        ▼
\\.\pipe\RgbFxLampArray
        │
        ▼
RgbFx.Service  — parse reports (Protocol) → map lamps → zones
        │
        ▼
POST http://<msi-host>:17700/api/v1/frame
        │
        ▼
msi-mystic-light-web → MSI Mystic Light HID
```

## Components

| Project | Role |
|---------|------|
| `RgbFx.LampArray.Protocol` | HID usages, report codec, descriptor bytes, models |
| `RgbFx.Service` | Config, HTTP client, zone mapper, sources (pipe/sim), sink |
| `RgbFx.UI` | **Only** remote target management + start/stop |
| `RgbFx.Driver` | VHF virtual device (WDK); skeleton → full bring-up |

## Source modes

| Mode | Behavior |
|------|----------|
| `pipe` | Read host updates from driver named pipe |
| `simulator` | Software rainbow for API path testing |
| `auto` | Prefer pipe (retries); set `RGBFX_FORCE_SIM=1` to force simulator |

## Mapping policy

Default: lamp `i` → UI zone list `[i % n]`, last-write wins when multiple lamps map to one zone.  
See `ZONE_MAPPING.md`.
