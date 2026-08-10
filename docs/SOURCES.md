# Lighting source modes

All modes are selectable. Only one is active at a time.

| SourceMode | What it does | Needs |
|------------|--------------|--------|
| `auto` | Prefer LampArray named pipe; retry. Force sim with `RGBFX_FORCE_SIM=1` | Optional driver |
| `pipe` | Windows Dynamic Lighting path via `\\.\pipe\RgbFxLampArray` | VHF LampArray driver |
| `simulator` | Software rainbow frames (no protocol) | Nothing |
| `aura-addressable-sim` | Aura Addressable device-side parser; pipe `RgbFxAuraAddressable` or protocol demo | Optional HID driver |

## Choosing

- **Debug MSI API only** → `simulator`
- **Windows Settings Dynamic Lighting** → `pipe` / `auto` + LampArray driver
- **Experiment Aura Addressable protocol / future 奥创 path** → `aura-addressable-sim`

Forwarding always ends at remote MSI: `POST /api/v1/frame`.
