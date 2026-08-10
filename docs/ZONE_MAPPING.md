# Zone mapping

Default mapping when remote reports zones `JRGB1, JRAINBOW1, JRAINBOW2, ONBOARD`:

| Lamp ID | Default zone |
|--------:|--------------|
| 0 | JRGB1 |
| 1 | JRAINBOW1 |
| 2 | JRAINBOW2 |
| 3 | ONBOARD |
| n | zones[n % count] |

Policy when multiple lamps hit one zone: **last write wins** in that frame.

Brightness: lamp intensity 0–255 → zone brightness 0–100.
