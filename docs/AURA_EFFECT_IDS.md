# Aura effect / mode keys

Source: `C:\ProgramData\ASUS\RogAura30\GetDeviceCap.xml` (local dump).

| mode key | Name | cansetcolor | cansetspeed | cansetdirection |
|---------:|------|:-----------:|:-----------:|:---------------:|
| 1 | Static | 1 | 0 | 0 |
| 2 | Breathing | 1 | 0 | 0 |
| 3 | Strobing (Mainboard list) | 1 | 0 | 0 |
| 4 | Color cycle | 0 | 0 | 0 |
| 5 | Rainbow | 0 | 1 | 0 |
| 8 | Comet | 1 | 1 | 1 |
| 10 | Flash and Dash | 1 | 1 | 1 |
| 11 | Wave | 0 | 1 | 0 |
| 12 | Glowing Yoyo | 0 | 1 | 0 |
| 13 | Starry-Night | 0 | 1 | 0 |
| 17 | Strobing (Group list) | 1 | 0 | 0 |
| 18 | Smart | 0 | 0 | 0 |
| 19 | Music | 0 | 0 | 0 |
| 21 | Select Effect | 0 | 0 | 0 |
| 101 | Off | 0 | 0 | 0 |

`IAacLedDevice.SetEffect(effectId, colors, numberOfColors)` — `colors` is `uint32*`.
