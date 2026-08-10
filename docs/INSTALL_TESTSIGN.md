# Install with test signing (development)

## Prerequisites

- Windows 11 with Dynamic Lighting available
- .NET 8 runtime/SDK
- For driver: VS 2022 + WDK
- Remote host running [msi-mystic-light-web](https://github.com/edfwef2w2/msi-mystic-light-web) with `/api/v1`

## A. API-only path (no driver)

1. `dotnet run --project src/RgbFx.UI`
2. Add target `http://<msi-ip>:17700`, **Probe**
3. Source mode **simulator**, **Start**
4. MSI lights should cycle (rainbow) via `/api/v1/frame`

Or:

```text
dotnet run --project src/RgbFx.Service -- --start --simulator
```

## B. Near-native Dynamic Lighting path

1. Admin: `bcdedit /set testsigning on` → reboot  
2. Build driver: `.\scripts\build-driver.ps1`  
3. Install INF (pnputil) — see `packaging/install.ps1`  
4. Confirm device under **Settings → Personalization → Dynamic lighting**  
5. UI: Source mode **pipe**, set remote target, **Start**  
6. Change colors in Windows Settings → should POST frames to MSI

## C. Optional API token

On MSI host:

```bash
export MSI_RGB_API_TOKEN=secret
```

In UI token field, enter the same secret (sent as Bearer).
