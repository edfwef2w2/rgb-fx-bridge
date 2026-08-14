# Architecture

```
Windows Dynamic Lighting (pipe / simulator)     Armoury Crate (AAC HAL LocalServer)
                    \                                   /
                     \                                 /
                      ▼                               ▼
                 RgbFx.UI  (one mode visible at a time; Debug below)
                      │
                      ▼
                 RgbFx.Service  →  POST /api/v1/frame
                      │
                      ▼
              msi-mystic-light-web
```

UI modes are exclusive on screen (`dynamic` | `aura`). HAL may still run as a COM LocalServer.

Strings live in `src/RgbFx.UI/I18n/*.json` (keys only in C#).

## Components

| Project | Role |
|---------|------|
| `RgbFx.LampArray.Protocol` | HID usages, report codec, descriptor bytes, models |
| `RgbFx.Service` | Config, HTTP client, zone mapper, sources (pipe/sim), sink |
| `RgbFx.UI` | Mode switch, MSI target, Aura install/uninstall, Debug |
| `RgbFx.AacHal` | Aura COM HAL → `/api/v1/frame` (URL from `msi-url.txt`) |
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
