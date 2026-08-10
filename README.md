# rgb-fx-bridge

Windows bridge that presents a **virtual HID LampArray** (Microsoft Dynamic Lighting path) and forwards colors to a remote [msi-mystic-light-web](https://github.com/edfwef2w2/msi-mystic-light-web) host via stable **`/api/v1/frame`**.

UI is intentionally minimal: **choose remote target, probe, start/stop**. Effects come from Windows Dynamic Lighting (or a software simulator for testing).

## Features

- Stable HTTP client for MSI `/api/v1` (health, capabilities, zones, frame)
- LampArray protocol library (report IDs, multi/range/control parse, descriptor)
- VHF KMDF driver skeleton + INF (test-sign install path)
- Named pipe contract `\\.\pipe\RgbFxLampArray`
- Simulator mode for end-to-end API validation without WDK
- **Experimental (branch `feature/aura-addressable-sim`):** Aura Addressable device-side sim (`aura-addressable-sim`) — clean-room HID reports from public OpenRGB wiki; coexists with `simulator` / `pipe`
- GitHub Actions CI for managed build/test/publish

See [docs/SOURCES.md](docs/SOURCES.md) and [docs/AURA_ADDRESSABLE_SIM.md](docs/AURA_ADDRESSABLE_SIM.md).

## Quick start (API path)

```powershell
# Requires .NET 8 SDK
dotnet restore src/RgbFxBridge.sln
dotnet test
dotnet run --project src/RgbFx.UI
```

1. Add `http://<msi-host>:17700`
2. Probe
3. Source = `simulator` → Start

## Dynamic Lighting device path

See [docs/INSTALL_TESTSIGN.md](docs/INSTALL_TESTSIGN.md) and [docs/LAMPARRAY_SPEC.md](docs/LAMPARRAY_SPEC.md).

## Layout

```
src/RgbFx.LampArray.Protocol       # Windows LampArray HID codec
src/RgbFx.AuraAddressable.Protocol # Aura Addressable device-side (experimental)
src/RgbFx.Service                  # host + HTTP sink + multi Source
src/RgbFx.UI                       # target picker + SourceMode
src/RgbFx.Driver                    # VHF skeleton (WDK)
docs/                               # architecture & install
.github/workflows/build.yml         # CI
```

## Related

- Server API: https://github.com/edfwef2w2/msi-mystic-light-web/blob/main/docs/API_v1.md
- Microsoft Dynamic Lighting devices: https://learn.microsoft.com/windows-hardware/design/component-guidelines/dynamic-lighting-devices

## License

MIT
