# LampArray implementation checklist

Aligned with Microsoft Dynamic Lighting device guidance and HID Lighting page (0x59).

| Requirement | Status | Notes |
|-------------|--------|-------|
| HID LampArray usage page | Protocol + descriptor | Shared C/C# descriptor |
| Device kind Chassis | Yes | `LampArrayKind.Chassis` |
| Lamp attributes / positions | Protocol models | Default 4-lamp chassis layout |
| Multi-Update report | Parse implemented | Report ID 0x04 |
| Range-Update report | Parse implemented | Report ID 0x05 |
| Control / Autonomous | Parse + sink policy | master off on autonomous |
| Appear in Settings → Dynamic lighting | Driver bring-up | Needs VHF complete + test sign |
| Forward to remote MSI API | Service HttpApiSink | `/api/v1/frame` |
| WHQL / production signing | Out of scope v1 | Test signing only |

## Official references

- https://learn.microsoft.com/windows-hardware/design/component-guidelines/dynamic-lighting-devices
- https://learn.microsoft.com/windows-hardware/drivers/hid/virtual-hid-framework--vhf-
- https://github.com/microsoft/ArduinoHidForWindows
- https://learn.microsoft.com/windows/apps/develop/devices-sensors/lighting-dynamic-lamparray
