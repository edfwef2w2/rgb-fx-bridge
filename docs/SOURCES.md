# Lighting source modes

All modes are selectable. Only one is active at a time.

| SourceMode | What it does | Needs |
|------------|--------------|--------|
| `auto` | Prefer LampArray named pipe; fall back to software simulator | Optional driver |
| `pipe` | Windows Dynamic Lighting path via `\\.\pipe\RgbFxLampArray` | VHF LampArray driver |
| `simulator` | Software rainbow frames (no HID driver) | Nothing |

## Choosing

- **Debug MSI API only** → `simulator`
- **Windows Settings Dynamic Lighting** → `pipe` / `auto` + LampArray driver

Force simulator: environment variable `RGBFX_FORCE_SIM=1`.

Forwarding always ends at remote MSI: `POST /api/v1/frame`.
