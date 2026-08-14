# rgb-fx-bridge

Windows bridge that presents a **virtual HID LampArray** (Microsoft **Dynamic Lighting** path) and forwards colors to a remote [msi-mystic-light-web](https://github.com/edfwef2w2/msi-mystic-light-web) host via stable **`/api/v1/frame`**.

UI is intentionally minimal: **choose remote target, probe, start/stop**. Effects come from Windows Dynamic Lighting (or a software simulator for testing).

## Features

- Stable HTTP client for MSI `/api/v1` (health, capabilities, zones, frame)
- LampArray protocol library (report IDs, multi/range/control parse, descriptor)
- VHF KMDF driver skeleton + INF (test-sign install path)
- Named pipe contract `\\.\pipe\RgbFxLampArray`
- Simulator mode for end-to-end API validation without WDK
- Auto fallback: pipe → simulator when the LampArray driver is not present
- GitHub Actions CI for managed build/test/publish

See [docs/SOURCES.md](docs/SOURCES.md).

## Quick start (API path)

```powershell
# Requires .NET 8 SDK
dotnet restore src/RgbFxBridge.sln
dotnet test
dotnet run --project src/RgbFx.UI
```

1. Add `http://<msi-host>:17700`
2. Probe
3. Source = `simulator` or `auto` → Start

## Dynamic Lighting device path

See [docs/INSTALL_TESTSIGN.md](docs/INSTALL_TESTSIGN.md) and [docs/LAMPARRAY_SPEC.md](docs/LAMPARRAY_SPEC.md).

Windows: **Settings → Personalization → Dynamic lighting**.

## Layout

```
src/RgbFx.LampArray.Protocol  # Windows LampArray HID codec
src/RgbFx.Service             # host + HTTP sink + multi Source
src/RgbFx.UI                  # target picker, Aura HAL add/remove (C#)
src/RgbFx.Setup               # Program Files copier (admin)
src/RgbFx.AacHal              # AAC HAL sources
src/RgbFx.Driver              # VHF skeleton (WDK)
docs/                         # architecture
.github/workflows/build.yml   # CI → portable zip + setup zip
```

CI artifacts:

- `RgbFxBridge-portable-win-x64.zip` — unzip and run `app\RgbFx.UI.exe`
- `RgbFxBridge-setup-win-x64.zip` — run `RgbFx.Setup.exe` (Program Files, Start Menu, Settings uninstall)

## Related

- Server API: https://github.com/edfwef2w2/msi-mystic-light-web/blob/main/docs/API_v1.md
- Microsoft Dynamic Lighting devices: https://learn.microsoft.com/windows-hardware/design/component-guidelines/dynamic-lighting-devices

## License

MIT
